using System;
using System.IO;
using UnityEngine;

namespace KillRitual.Core.SaveData
{
    public sealed class KRFileManager
    {
        // 모든 세이브 파일은 플랫폼별 영구 저장 경로(Application.persistentDataPath) 하위의
        // "SaveData" 폴더에 모아 관리합니다. (에디터/PC/콘솔/모바일에서 모두 쓰기 가능한 표준 경로)
        private readonly string _saveDirectory;

        public KRFileManager()
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, "SaveData");
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }

        public bool Save<T>(string fileName, T data)
        {
            try
            {
                EnsureDirectoryExists();

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string path = GetFullPath(fileName);
                string tempPath = path + ".tmp";

                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KRFileManager] '{fileName}' 저장 실패: {exception.Message}");
                return false;
            }
        }

        public T Load<T>(string fileName, T defaultValue = default)
        {
            try
            {
                string path = GetFullPath(fileName);

                if (!File.Exists(path))
                {
                    return defaultValue;
                }

                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KRFileManager] '{fileName}' 로드 실패: {exception.Message}");
                return defaultValue;
            }
        }

        public bool TryLoad<T>(string fileName, out T result)
        {
            string path = GetFullPath(fileName);

            if (!File.Exists(path))
            {
                result = default;
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                result = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KRFileManager] '{fileName}' 로드 실패: {exception.Message}");
                result = default;
                return false;
            }
        }

        public bool Exists(string fileName)
        {
            return File.Exists(GetFullPath(fileName));
        }

        public bool Delete(string fileName)
        {
            try
            {
                string path = GetFullPath(fileName);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KRFileManager] '{fileName}' 삭제 실패: {exception.Message}");
                return false;
            }
        }

        private string GetFullPath(string fileName)
        {
            return Path.Combine(_saveDirectory, fileName + ".json");
        }
    }
}
