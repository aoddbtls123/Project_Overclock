using System;
using System.IO;
using UnityEngine;

namespace Overclock.Core
{
    /// <summary>
    /// 모드별 최고 기록을 JSON 파일 1개로 저장하고 읽는다. 런 세이브(RunSaveFile)와 따로 둔다 — 런 세이브는 런이 끝나면 지워진다.
    /// </summary>
    public sealed class BestRecordFile
    {
        public const string FileName = "best_records.json";

        private readonly string directory;

        public BestRecordFile(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("기록 폴더가 비어 있습니다.", nameof(directory));
            }

            this.directory = directory;
            FilePath = Path.Combine(directory, FileName);
        }

        public string FilePath { get; }

        public void Save(BestRecords records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            Directory.CreateDirectory(directory);

            // 임시 파일에 다 쓴 뒤 바꾼다. 쓰는 도중 꺼져도 이전 기록이 온전히 남는다
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(records.ToData(), true));
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
        /// 기록을 읽는다. 파일이 없으면 빈 기록과 true. 깨졌거나 게임이 만들 수 없는 값이면 빈 기록과 false, 이유를 돌려준다.
        /// </summary>
        public bool TryLoad(out BestRecords records, out string error)
        {
            records = new BestRecords();
            error = null;
            if (!File.Exists(FilePath))
            {
                return true;
            }

            try
            {
                // 빈 파일이면 null 이 온다. FromData 가 null 을 ArgumentNullException 으로 거부한다
                records = BestRecords.FromData(JsonUtility.FromJson<BestRecordData>(File.ReadAllText(FilePath)));
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

            records = new BestRecords();
            return false;
        }
    }
}
