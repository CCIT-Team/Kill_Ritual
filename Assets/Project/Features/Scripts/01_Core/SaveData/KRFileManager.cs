using System;
using System.IO;
using UnityEngine;

namespace KillRitual.Core.SaveData
{
    /// <summary>
    /// JSON 직렬화 기반의 범용 파일 입출력을 담당하는 매니저입니다.
    /// 세이브 대상 데이터 타입(class 또는 [System.Serializable] struct)에 의존하지 않는
    /// 제네릭 구조로 설계되어, 옵션값/플레이어 진행도/로드아웃 등 다양한 데이터를 저장하는 데
    /// 재사용할 수 있습니다.
    ///
    /// 사용 예시:
    ///   [System.Serializable]
    ///   private struct KRPlayerSettings { public float MasterVolume; public float MouseSensitivity; }
    ///
    ///   var settings = new KRPlayerSettings { MasterVolume = 0.8f, MouseSensitivity = 2.5f };
    ///   KRManagers.File.Save("player_settings", settings);
    ///   var loaded = KRManagers.File.Load("player_settings", new KRPlayerSettings { MasterVolume = 1f, MouseSensitivity = 1f });
    /// </summary>
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

        /// <summary>
        /// 지정한 fileName(확장자 제외)에 데이터를 JSON으로 직렬화하여 저장합니다.
        /// 데이터 타입 T는 JsonUtility 직렬화를 위해 [System.Serializable]이 지정되어 있어야 합니다.
        /// 저장 도중 강제 종료 등으로 파일이 손상되는 것을 막기 위해, 임시 파일(.tmp)에 먼저 쓴 뒤
        /// 쓰기가 완전히 끝나면 기존 파일을 교체하는 방식(원자적 쓰기)을 사용합니다.
        /// </summary>
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

        /// <summary>
        /// 지정한 fileName의 JSON 파일을 읽어 T 타입으로 역직렬화합니다.
        /// 파일이 없거나 손상되어 읽기에 실패하면 defaultValue를 그대로 반환하므로,
        /// 호출부에서 별도의 null 체크 없이 안전하게 사용할 수 있습니다.
        /// </summary>
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

        /// <summary>
        /// out 파라미터 버전의 Load입니다. 로드 성공 여부를 bool로 명시적으로 반환하므로,
        /// "세이브 파일이 아예 없는 최초 실행"과 "기본값이 우연히 저장된 경우"를 구분해야 하는
        /// 로직(예: 신규 유저 튜토리얼 분기)에서 유용합니다.
        /// </summary>
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

        /// <summary>해당 이름의 세이브 파일이 존재하는지 확인합니다.</summary>
        public bool Exists(string fileName)
        {
            return File.Exists(GetFullPath(fileName));
        }

        /// <summary>해당 이름의 세이브 파일을 삭제합니다. 파일이 없으면 아무 동작 없이 true를 반환합니다.</summary>
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
