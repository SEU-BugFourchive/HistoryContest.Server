﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HistoryContest.Server.Models.Entities;

namespace HistoryContest.Server.Data
{
    public static class DbInitializer
    {
        /// <summary>
        /// Seed Test Data
        /// </summary>
        public static void Initialize(ContestContext context)
        {
            context.Database.EnsureCreated();

            // Look for any students.
            if (context.Students.Any())
            {
                return;   // DB has been seeded
            }

            var students = new Student[]
            {
                new Student { ID = 09016319, Name = "叶志浩", CardID = username, CounselorID = 1 },
                new Student { ID = 09016407, Name = "胡黛琳", CardID = username, CounselorID = 1 }
            };
            foreach (Student s in students)
            {
                context.Students.Add(s);
            }

            var counselors = new Counselor[]
            {
                new Counselor { ID = 1, Name = "郭佳", Department = Department.CS }
            };
            foreach (Counselor c in counselors)
            {
                context.Counselors.Add(c);
            }

            System.Collections.BitArray a = new System.Collections.BitArray(500);

            context.SaveChanges();
        }
    }
}
