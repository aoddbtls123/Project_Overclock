using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public sealed class PartCatalog
    {
        private readonly RandomService randomService;
        private readonly List<PartData>[] partsByGrade;

        public PartCatalog(IReadOnlyList<PartData> parts, RandomService randomService)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            if (randomService == null)
            {
                throw new ArgumentNullException(nameof(randomService));
            }

            this.randomService = randomService;

            int gradeCount = Enum.GetValues(typeof(Grade)).Length;
            partsByGrade = new List<PartData>[gradeCount];
            for (int i = 0; i < gradeCount; i++)
            {
                partsByGrade[i] = new List<PartData>();
            }

            foreach (PartData part in parts)
            {
                if (part == null)
                {
                    throw new ArgumentException("부품 목록에 비어 있는 항목이 있습니다.", nameof(parts));
                }

                partsByGrade[(int)part.Grade].Add(part);
            }

            for (int i = 0; i < gradeCount; i++)
            {
                if (partsByGrade[i].Count == 0)
                {
                    throw new ArgumentException((Grade)i + " 등급 부품이 없습니다.", nameof(parts));
                }
            }
        }

        public PartData Pick(Grade grade)
        {
            return Select(grade, randomService.NextDouble());
        }

        public PartData Select(Grade grade, double roll)
        {
            List<PartData> candidates = partsByGrade[(int)grade];
            int index = (int)(roll * candidates.Count);
            return candidates[index];
        }
    }
}
