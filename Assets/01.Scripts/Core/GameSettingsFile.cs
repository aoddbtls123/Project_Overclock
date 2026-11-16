using System;
using System.IO;
using UnityEngine;

namespace Overclock.Core
{
    /// <summary>
    /// 설정을 JSON 파일 1개로 저장하고 읽음. 최고 기록 파일(BestRecordFile)과 같은 규칙 — 임시 파일에 쓴 뒤 바꿈.
    /// </summary>
    public sealed class GameSettingsFile
    {
        public const string FileName = "settings.json";

        private readonly string directory;

        public GameSettingsFile(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("설정 폴더가 비어 있습니다.", nameof(directory));
            }

            this.directory = directory;
            FilePath = Path.Combine(directory, FileName);
        }

        public string FilePath { get; }

        public void Save(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(directory);

            // 임시 파일에 다 쓴 뒤 바꿈. 쓰는 도중 꺼져도 이전 설정이 온전히 남음
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(settings.ToData(), true));
            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, null);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }
        }

        /// <summary>
        /// 설정을 읽음. 파일이 없으면 기본 설정과 true. 깨졌거나 게임이 만들 수 없는 값이면 기본 설정과 false, 이유를 돌려줌.
        /// </summary>
        public bool TryLoad(out GameSettings settings, out string error)
        {
            settings = new GameSettings();
            error = null;
            if (!File.Exists(FilePath))
            {
                return true;
            }

            try
            {
                // 빈 파일이면 null 이 옴. FromData 가 null 을 ArgumentNullException 으로 거부함
                settings = GameSettings.FromData(JsonUtility.FromJson<GameSettingsData>(File.ReadAllText(FilePath)));
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
            }
            catch (IOException exception)
            {
                error = exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = exception.Message;
            }

            // 실패하면 settings 는 처음에 만든 기본 설정 그대로임
            return false;
        }
    }
}
