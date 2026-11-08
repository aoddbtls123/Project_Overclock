using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overclock.Core
{
    /// <summary>
    /// 진행 중인 런 하나를 JSON 파일 1개로 저장하고 읽는다. 폴더는 밖에서 받는다 (게임은 persistentDataPath, 테스트는 임시 폴더).
    /// </summary>
    public sealed class RunSaveFile
    {
        public const string FileName = "run_save.json";

        private readonly string directory;

        public RunSaveFile(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("세이브 폴더가 비어 있습니다.", nameof(directory));
            }

            this.directory = directory;
            FilePath = Path.Combine(directory, FileName);
        }

        public string FilePath { get; }
        public bool Exists => File.Exists(FilePath);

        public void Save(RunSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Write(session.CaptureSaveData());
        }

        internal void Write(RunSaveData data)
        {
            Directory.CreateDirectory(directory);

            // 임시 파일에 다 쓴 뒤 바꾼다. 쓰는 도중 꺼져도 이전 세이브가 온전히 남는다
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
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
        /// 세이브를 읽어 런을 되살린다. 파일이 없거나, 깨졌거나, 지금 데이터로 복원할 수 없으면 false 와 이유를 돌려준다.
        /// </summary>
        public bool TryLoad(IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, out RunSession session, out string error)
        {
            return TryLoad(parts, modules, null, out session, out error);
        }

        public bool TryLoad(IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules,
            ShopSettings shop, out RunSession session, out string error)
        {
            session = null;
            try
            {
                // 빈 파일이면 null 이 온다. Restore 가 null 을 ArgumentNullException 으로 거부한다
                RunSaveData data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
                session = RunSession.Restore(data, parts, modules, shop);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                // JSON 형식 오류와 복원 검사 실패가 모두 여기로 온다
                error = exception.Message;
                return false;
            }
            catch (IOException exception)
            {
                // 파일이 없을 때(FileNotFoundException)도 여기로 온다
                error = exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
