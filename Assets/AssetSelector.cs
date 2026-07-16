#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KillRitual.EditorTools
{
    /// <summary>
    /// PPA 제출용 최소 Unity 프로젝트 ZIP 생성기.
    ///
    /// ZIP 내부 구조:
    /// - Assets            : Build Settings 씬 Dependencies만 복사
    /// - ProjectSettings   : 전체 복사
    /// - Packages          : 전체 복사
    ///
    /// 원본 프로젝트의 파일은 수정하거나 삭제하지 않습니다.
    /// </summary>
    public static class KRPPAProjectZipExporter
    {
        private const string DefaultZipName =
            "KillRitual_PPA_Minimal";

        private const string StagingFolderName =
            "KRPPA_MinimalProjectExport";

        // 안전 모드에서 추가로 포함할 파일 확장자.
        // 스크립트 상속·인터페이스·Shader.Find 등은
        // 씬 Dependency만으로 완전히 추적되지 않을 수 있습니다.
        private static readonly HashSet<string> SafeExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // C# 및 어셈블리
                ".cs",
                ".asmdef",
                ".asmref",
                ".rsp",
                ".dll",
                ".pdb",
                ".mdb",

                // 셰이더
                ".shader",
                ".shadergraph",
                ".shadersubgraph",
                ".cginc",
                ".hlsl",
                ".compute",
                ".shadervariants",

                // 입력
                ".inputactions",

                // 네이티브·모바일 플러그인
                ".aar",
                ".jar",
                ".so",
                ".a",
                ".bundle"
            };

        /// <summary>
        /// 권장 모드.
        ///
        /// 씬 Dependencies 외에 다음도 포함:
        /// - 모든 C# 및 셰이더
        /// - Resources
        /// - StreamingAssets
        /// - Plugins
        /// - AddressableAssetsData
        /// - Gizmos
        /// - Editor Default Resources
        ///
        /// 동적 로딩 때문에 누락되는 문제를 줄입니다.
        /// </summary>
        [MenuItem(
            "Tools/KillRitual/PPA/Export Minimal Project ZIP - Safe",
            priority = 100)]
        private static void ExportSafeZip()
        {
            ExportProjectZip(includeSafetyAssets: true);
        }

        /// <summary>
        /// 엄격 모드.
        ///
        /// Build Settings 씬과 현재 선택한 추가 루트의
        /// AssetDatabase Dependencies만 포함합니다.
        ///
        /// Resources.Load, Shader.Find, 문자열 기반 로딩 등의
        /// 동적 참조는 누락될 수 있습니다.
        /// </summary>
        [MenuItem(
            "Tools/KillRitual/PPA/Export Minimal Project ZIP - Strict",
            priority = 101)]
        private static void ExportStrictZip()
        {
            ExportProjectZip(includeSafetyAssets: false);
        }

        private static void ExportProjectZip(
            bool includeSafetyAssets)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            AssetDatabase.SaveAssets();

            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                ShowDialog(
                    "PPA ZIP 생성 실패",
                    "현재 Unity 프로젝트의 루트 경로를 확인할 수 없습니다.");

                return;
            }

            // Build Settings 씬 및 현재 Project 창 선택 에셋을 루트로 수집.
            HashSet<string> rootAssetPaths =
                CollectRootAssetPaths(projectRoot);

            if (rootAssetPaths.Count == 0)
            {
                ShowDialog(
                    "루트 에셋 없음",
                    "Build Settings에 활성화된 씬이 없습니다.\n\n" +
                    "File → Build Settings에서 제출할 씬을 모두 등록하고\n" +
                    "체크한 뒤 다시 실행하세요.\n\n" +
                    "동적 로딩 에셋은 Project 창에서 추가 선택할 수 있습니다.");

                return;
            }

            string zipPath =
                EditorUtility.SaveFilePanel(
                    "PPA 최소 프로젝트 ZIP 저장",
                    projectRoot,
                    DefaultZipName,
                    "zip");

            if (string.IsNullOrWhiteSpace(zipPath))
                return;

            string stagingRoot =
                Path.Combine(
                    projectRoot,
                    "Library",
                    StagingFolderName);

            string stagingProjectRoot =
                Path.Combine(
                    stagingRoot,
                    DefaultZipName);

            bool exportSucceeded = false;

            try
            {
                PrepareEmptyStagingDirectory(
                    stagingRoot,
                    stagingProjectRoot);

                EditorUtility.DisplayProgressBar(
                    "PPA 최소 프로젝트 생성",
                    "Build Settings 씬의 Dependencies를 계산하는 중입니다.",
                    0.05f);

                HashSet<string> usedAssetPaths =
                    CollectDependencyPaths(
                        rootAssetPaths,
                        projectRoot);

                int dependencyOnlyCount =
                    usedAssetPaths.Count;

                int safetyAssetCount = 0;

                if (includeSafetyAssets)
                {
                    EditorUtility.DisplayProgressBar(
                        "PPA 최소 프로젝트 생성",
                        "동적 로딩 및 코드 안전 파일을 확인하는 중입니다.",
                        0.15f);

                    safetyAssetCount =
                        AddSafetyAssets(
                            usedAssetPaths,
                            projectRoot);
                }

                EditorUtility.DisplayProgressBar(
                    "PPA 최소 프로젝트 생성",
                    $"Assets 파일 {usedAssetPaths.Count:N0}개를 복사하는 중입니다.",
                    0.25f);

                CopyFilteredAssets(
                    projectRoot,
                    stagingProjectRoot,
                    usedAssetPaths);

                EditorUtility.DisplayProgressBar(
                    "PPA 최소 프로젝트 생성",
                    "ProjectSettings 폴더를 복사하는 중입니다.",
                    0.65f);

                CopyWholeProjectFolder(
                    Path.Combine(projectRoot, "ProjectSettings"),
                    Path.Combine(stagingProjectRoot, "ProjectSettings"));

                EditorUtility.DisplayProgressBar(
                    "PPA 최소 프로젝트 생성",
                    "Packages 폴더를 복사하는 중입니다.",
                    0.72f);

                CopyWholeProjectFolder(
                    Path.Combine(projectRoot, "Packages"),
                    Path.Combine(stagingProjectRoot, "Packages"));

                ValidateExportStructure(
                    stagingProjectRoot);

                EditorUtility.DisplayProgressBar(
                    "PPA 최소 프로젝트 생성",
                    "ZIP 파일을 생성하는 중입니다.",
                    0.8f);

                CreateZip(
                    stagingProjectRoot,
                    zipPath);

                long zipSize =
                    File.Exists(zipPath)
                        ? new FileInfo(zipPath).Length
                        : 0L;

                exportSucceeded = true;

                Debug.Log(
                    "[KRPPAProjectZipExporter] PPA ZIP 생성 완료\n" +
                    $"모드: {(includeSafetyAssets ? "Safe" : "Strict")}\n" +
                    $"루트 에셋: {rootAssetPaths.Count:N0}개\n" +
                    $"Dependency 파일: {dependencyOnlyCount:N0}개\n" +
                    $"안전 추가 파일: {safetyAssetCount:N0}개\n" +
                    $"최종 Assets 파일: {usedAssetPaths.Count:N0}개\n" +
                    $"ZIP 용량: {FormatFileSize(zipSize)}\n" +
                    $"저장 경로: {zipPath}");

                ShowDialog(
                    "PPA ZIP 생성 완료",
                    $"모드: {(includeSafetyAssets ? "Safe" : "Strict")}\n\n" +
                    $"루트 에셋: {rootAssetPaths.Count:N0}개\n" +
                    $"Dependency 파일: {dependencyOnlyCount:N0}개\n" +
                    $"안전 추가 파일: {safetyAssetCount:N0}개\n" +
                    $"최종 Assets 파일: {usedAssetPaths.Count:N0}개\n" +
                    $"ZIP 용량: {FormatFileSize(zipSize)}\n\n" +
                    "ZIP 내부에는 다음 세 폴더만 들어 있습니다.\n" +
                    "Assets\n" +
                    "ProjectSettings\n" +
                    "Packages\n\n" +
                    zipPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                ShowDialog(
                    "PPA ZIP 생성 실패",
                    exception.Message +
                    "\n\n임시 폴더:\n" +
                    stagingProjectRoot);
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // 성공했을 때만 임시 프로젝트를 삭제.
                // 실패한 경우에는 원인 확인을 위해 남겨둡니다.
                if (exportSucceeded)
                {
                    TryDeleteDirectory(stagingRoot);
                }
            }
        }

        // ====================================================================
        // 루트 에셋 수집
        // ====================================================================

        private static HashSet<string> CollectRootAssetPaths(
            string projectRoot)
        {
            HashSet<string> roots =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            // 1. Build Settings에서 활성화된 모든 씬.
            foreach (EditorBuildSettingsScene buildScene
                     in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                    continue;

                string scenePath =
                    NormalizeAssetPath(buildScene.path);

                if (!IsExistingAssetFile(
                        scenePath,
                        projectRoot))
                {
                    Debug.LogWarning(
                        "[KRPPAProjectZipExporter] " +
                        "Build Settings 씬을 찾을 수 없습니다: " +
                        scenePath);

                    continue;
                }

                roots.Add(scenePath);
            }

            // 2. Project 창에서 추가로 선택한 에셋.
            //
            // Resources.Load, Addressables, 문자열 로딩 등의 에셋은
            // 실행 전에 선택해 두면 추가 루트로 포함할 수 있습니다.
            foreach (UnityEngine.Object selectedObject
                     in Selection.objects)
            {
                if (selectedObject == null)
                    continue;

                string selectedPath =
                    NormalizeAssetPath(
                        AssetDatabase.GetAssetPath(selectedObject));

                if (!IsAssetsPath(selectedPath))
                    continue;

                if (AssetDatabase.IsValidFolder(selectedPath))
                {
                    string[] guids =
                        AssetDatabase.FindAssets(
                            string.Empty,
                            new[] { selectedPath });

                    foreach (string guid in guids)
                    {
                        string childPath =
                            NormalizeAssetPath(
                                AssetDatabase.GUIDToAssetPath(guid));

                        if (IsExistingAssetFile(
                                childPath,
                                projectRoot))
                        {
                            roots.Add(childPath);
                        }
                    }
                }
                else if (IsExistingAssetFile(
                             selectedPath,
                             projectRoot))
                {
                    roots.Add(selectedPath);
                }
            }

            return roots;
        }

        // ====================================================================
        // Dependency 계산
        // ====================================================================

        private static HashSet<string> CollectDependencyPaths(
            HashSet<string> rootAssetPaths,
            string projectRoot)
        {
            string[] dependencies =
                AssetDatabase.GetDependencies(
                    rootAssetPaths.ToArray(),
                    recursive: true);

            HashSet<string> result =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string dependency in dependencies)
            {
                string normalizedPath =
                    NormalizeAssetPath(dependency);

                // Packages 내부 에셋은 Packages/manifest.json을 통해
                // Package Manager가 복원하므로 Assets 파일만 직접 복사.
                if (!IsExistingAssetFile(
                        normalizedPath,
                        projectRoot))
                {
                    continue;
                }

                result.Add(normalizedPath);
            }

            // 루트 에셋 자체도 반드시 포함.
            foreach (string rootPath in rootAssetPaths)
            {
                if (IsExistingAssetFile(
                        rootPath,
                        projectRoot))
                {
                    result.Add(rootPath);
                }
            }

            return result;
        }

        // ====================================================================
        // 안전 추가 파일
        // ====================================================================

        private static int AddSafetyAssets(
            HashSet<string> usedAssetPaths,
            string projectRoot)
        {
            int beforeCount =
                usedAssetPaths.Count;

            string[] allAssetPaths =
                AssetDatabase.GetAllAssetPaths();

            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath =
                    NormalizeAssetPath(allAssetPaths[i]);

                if (!IsExistingAssetFile(
                        assetPath,
                        projectRoot))
                {
                    continue;
                }

                if (IsSafetyAsset(assetPath))
                    usedAssetPaths.Add(assetPath);
            }

            return usedAssetPaths.Count - beforeCount;
        }

        private static bool IsSafetyAsset(
            string assetPath)
        {
            string extension =
                Path.GetExtension(assetPath);

            if (SafeExtensions.Contains(extension))
                return true;

            string fileName =
                Path.GetFileName(assetPath);

            if (string.Equals(
                    fileName,
                    "link.xml",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string normalized =
                "/" +
                NormalizeAssetPath(assetPath).Trim('/') +
                "/";

            return
                ContainsFolder(normalized, "/Resources/") ||
                ContainsFolder(normalized, "/StreamingAssets/") ||
                ContainsFolder(normalized, "/Plugins/") ||
                ContainsFolder(normalized, "/Gizmos/") ||
                ContainsFolder(normalized, "/Editor Default Resources/") ||
                ContainsFolder(normalized, "/AddressableAssetsData/");
        }

        private static bool ContainsFolder(
            string normalizedPath,
            string folderFragment)
        {
            return normalizedPath.IndexOf(
                       folderFragment,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ====================================================================
        // Assets 복사
        // ====================================================================

        private static void CopyFilteredAssets(
            string projectRoot,
            string stagingProjectRoot,
            HashSet<string> usedAssetPaths)
        {
            string targetAssetsDirectory =
                Path.Combine(
                    stagingProjectRoot,
                    "Assets");

            Directory.CreateDirectory(
                targetAssetsDirectory);

            string[] orderedPaths =
                usedAssetPaths
                    .OrderBy(path => path)
                    .ToArray();

            for (int i = 0; i < orderedPaths.Length; i++)
            {
                string assetPath =
                    orderedPaths[i];

                if (i % 25 == 0)
                {
                    float progress =
                        Mathf.Lerp(
                            0.25f,
                            0.65f,
                            orderedPaths.Length == 0
                                ? 1f
                                : (float)i / orderedPaths.Length);

                    EditorUtility.DisplayProgressBar(
                        "PPA 최소 프로젝트 생성",
                        assetPath,
                        progress);
                }

                CopyAssetAndMeta(
                    projectRoot,
                    stagingProjectRoot,
                    assetPath);

                CopyParentFolderMetas(
                    projectRoot,
                    stagingProjectRoot,
                    assetPath);
            }
        }

        private static void CopyAssetAndMeta(
            string projectRoot,
            string stagingProjectRoot,
            string assetPath)
        {
            string sourceFile =
                ToAbsoluteProjectPath(
                    projectRoot,
                    assetPath);

            string targetFile =
                ToAbsoluteProjectPath(
                    stagingProjectRoot,
                    assetPath);

            if (!File.Exists(sourceFile))
            {
                Debug.LogWarning(
                    "[KRPPAProjectZipExporter] " +
                    "복사할 파일이 없습니다: " +
                    assetPath);

                return;
            }

            CreateParentDirectory(targetFile);

            File.Copy(
                sourceFile,
                targetFile,
                overwrite: true);

            string sourceMeta =
                sourceFile + ".meta";

            string targetMeta =
                targetFile + ".meta";

            if (File.Exists(sourceMeta))
            {
                CreateParentDirectory(targetMeta);

                File.Copy(
                    sourceMeta,
                    targetMeta,
                    overwrite: true);
            }
            else
            {
                Debug.LogWarning(
                    "[KRPPAProjectZipExporter] " +
                    ".meta 파일이 없습니다: " +
                    assetPath);
            }
        }

        /// <summary>
        /// 에셋 파일뿐 아니라 상위 폴더의 .meta도 복사합니다.
        ///
        /// 예:
        /// Assets/Art/Model/Test.fbx
        ///
        /// Assets/Art.meta
        /// Assets/Art/Model.meta
        /// 를 함께 복사합니다.
        /// </summary>
        private static void CopyParentFolderMetas(
            string projectRoot,
            string stagingProjectRoot,
            string assetPath)
        {
            string folderPath =
                NormalizeAssetPath(
                    Path.GetDirectoryName(assetPath));

            while (!string.IsNullOrWhiteSpace(folderPath) &&
                   folderPath.StartsWith(
                       "Assets/",
                       StringComparison.OrdinalIgnoreCase))
            {
                string folderMetaRelativePath =
                    folderPath + ".meta";

                string sourceMeta =
                    ToAbsoluteProjectPath(
                        projectRoot,
                        folderMetaRelativePath);

                string targetMeta =
                    ToAbsoluteProjectPath(
                        stagingProjectRoot,
                        folderMetaRelativePath);

                if (File.Exists(sourceMeta) &&
                    !File.Exists(targetMeta))
                {
                    CreateParentDirectory(targetMeta);

                    File.Copy(
                        sourceMeta,
                        targetMeta,
                        overwrite: true);
                }

                folderPath =
                    NormalizeAssetPath(
                        Path.GetDirectoryName(folderPath));
            }
        }

        // ====================================================================
        // ProjectSettings / Packages 전체 복사
        // ====================================================================

        private static void CopyWholeProjectFolder(
            string sourceDirectory,
            string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException(
                    "필수 프로젝트 폴더를 찾을 수 없습니다:\n" +
                    sourceDirectory);
            }

            Directory.CreateDirectory(
                targetDirectory);

            foreach (string directory
                     in Directory.GetDirectories(
                         sourceDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath =
                    GetRelativePath(
                        sourceDirectory,
                        directory);

                Directory.CreateDirectory(
                    Path.Combine(
                        targetDirectory,
                        relativePath));
            }

            foreach (string sourceFile
                     in Directory.GetFiles(
                         sourceDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath =
                    GetRelativePath(
                        sourceDirectory,
                        sourceFile);

                string targetFile =
                    Path.Combine(
                        targetDirectory,
                        relativePath);

                CreateParentDirectory(
                    targetFile);

                File.Copy(
                    sourceFile,
                    targetFile,
                    overwrite: true);
            }
        }

        // ====================================================================
        // ZIP 생성
        // ====================================================================

        private static void CreateZip(
    string sourceProjectDirectory,
    string zipPath)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            string zipParent =
                Path.GetDirectoryName(zipPath);

            if (!string.IsNullOrWhiteSpace(zipParent))
                Directory.CreateDirectory(zipParent);

            string[] files =
                Directory.GetFiles(
                    sourceProjectDirectory,
                    "*",
                    SearchOption.AllDirectories);

            using (FileStream zipFileStream =
                   new FileStream(
                       zipPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (ZipArchive archive =
                   new ZipArchive(
                       zipFileStream,
                       ZipArchiveMode.Create))
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string sourceFile =
                        files[i];

                    string relativePath =
                        GetRelativePath(
                            sourceProjectDirectory,
                            sourceFile)
                        .Replace('\\', '/');

                    if (i % 25 == 0)
                    {
                        float progress =
                            Mathf.Lerp(
                                0.8f,
                                1f,
                                files.Length == 0
                                    ? 1f
                                    : (float)i / files.Length);

                        EditorUtility.DisplayProgressBar(
                            "PPA ZIP 생성",
                            relativePath,
                            progress);
                    }

                    ZipArchiveEntry entry =
                        archive.CreateEntry(
                            relativePath,
                            System.IO.Compression.CompressionLevel.Optimal);

                    using (FileStream sourceStream =
                           new FileStream(
                               sourceFile,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.Read))
                    using (Stream entryStream =
                           entry.Open())
                    {
                        sourceStream.CopyTo(entryStream);
                    }
                }
            }
        }

        // ====================================================================
        // 검증 및 유틸리티
        // ====================================================================

        private static void PrepareEmptyStagingDirectory(
            string stagingRoot,
            string stagingProjectRoot)
        {
            TryDeleteDirectory(stagingRoot);

            Directory.CreateDirectory(
                Path.Combine(
                    stagingProjectRoot,
                    "Assets"));

            Directory.CreateDirectory(
                Path.Combine(
                    stagingProjectRoot,
                    "ProjectSettings"));

            Directory.CreateDirectory(
                Path.Combine(
                    stagingProjectRoot,
                    "Packages"));
        }

        private static void ValidateExportStructure(
            string stagingProjectRoot)
        {
            string assets =
                Path.Combine(
                    stagingProjectRoot,
                    "Assets");

            string projectSettings =
                Path.Combine(
                    stagingProjectRoot,
                    "ProjectSettings");

            string packages =
                Path.Combine(
                    stagingProjectRoot,
                    "Packages");

            if (!Directory.Exists(assets))
                throw new DirectoryNotFoundException("Assets 폴더 생성 실패.");

            if (!Directory.Exists(projectSettings))
                throw new DirectoryNotFoundException("ProjectSettings 폴더 복사 실패.");

            if (!Directory.Exists(packages))
                throw new DirectoryNotFoundException("Packages 폴더 복사 실패.");

            string manifestPath =
                Path.Combine(
                    packages,
                    "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning(
                    "[KRPPAProjectZipExporter] " +
                    "Packages/manifest.json이 없습니다.");
            }
        }

        private static bool IsExistingAssetFile(
            string assetPath,
            string projectRoot)
        {
            if (!IsAssetsPath(assetPath))
                return false;

            if (string.Equals(
                    assetPath,
                    "Assets",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
                return false;

            if (assetPath.EndsWith(
                    ".meta",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string absolutePath =
                ToAbsoluteProjectPath(
                    projectRoot,
                    assetPath);

            return File.Exists(absolutePath);
        }

        private static bool IsAssetsPath(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return string.Equals(
                       assetPath,
                       "Assets",
                       StringComparison.OrdinalIgnoreCase) ||
                   assetPath.StartsWith(
                       "Assets/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsoluteProjectPath(
            string projectRoot,
            string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static string NormalizeAssetPath(
            string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        private static string GetRelativePath(
            string rootDirectory,
            string fullPath)
        {
            string normalizedRoot =
                Path.GetFullPath(rootDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            string normalizedFullPath =
                Path.GetFullPath(fullPath);

            if (!normalizedFullPath.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "경로가 기준 폴더 외부에 있습니다:\n" +
                    normalizedFullPath);
            }

            return normalizedFullPath
                .Substring(normalizedRoot.Length)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }

        private static void CreateParentDirectory(
            string filePath)
        {
            string parent =
                Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
        }

        private static void TryDeleteDirectory(
            string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;

            try
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[KRPPAProjectZipExporter] " +
                    "임시 폴더 삭제 실패:\n" +
                    directoryPath +
                    "\n" +
                    exception.Message);
            }
        }

        private static string FormatFileSize(
            long bytes)
        {
            double size = bytes;

            string[] units =
            {
                "B",
                "KB",
                "MB",
                "GB",
                "TB"
            };

            int unitIndex = 0;

            while (size >= 1024d &&
                   unitIndex < units.Length - 1)
            {
                size /= 1024d;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        private static void ShowDialog(
            string title,
            string message)
        {
            EditorUtility.DisplayDialog(
                title,
                message,
                "확인");
        }
    }
}

#endif