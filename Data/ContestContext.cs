﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;
using HistoryContest.Server.Models.Entities;
using HistoryContest.Server;
using Microsoft.AspNetCore.Hosting;
using HistoryContest.Server.Services;
using HistoryContest.Server.Extensions;

namespace HistoryContest.Server.Data
{
    public class ContestContext : DbContext
    {
        public ContestContext()
        {

        }

        public ContestContext(DbContextOptions<ContestContext> options) : base(options)
        {

        }

        #region Entity Sets
        public DbSet<Student> Students { get; set; }
        public DbSet<Counselor> Counselors { get; set; }
        public DbSet<Administrator> Administrators { get; set; }
        public DbSet<ChoiceQuestion> ChoiceQuestions { get; set; }
        public DbSet<TrueFalseQuestion> TrueFalseQuestions { get; set; }
        public DbSet<AQuestionBase> Questions { get; set; }
        public DbSet<QuestionSeed> QuestionSeeds { get; set; }
        public DbSet<DeptWuID> DepartmentWuIDs { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuestionSeed>()
                .Property(s => s._questionIDs).HasColumnName("QuestionIDs");

            modelBuilder.Entity<Counselor>()
                .HasIndex(c => c.Department);

        }

        public bool AllMigrationsApplied()
        {
            var applied = this.GetService<IHistoryRepository>()
                .GetAppliedMigrations()
                .Select(m => m.MigrationId);

            var total = this.GetService<IMigrationsAssembly>()
                .Migrations
                .Select(m => m.Key);

            return !total.Except(applied).Any();
        }

        public void EnsureSeeded<TEntity>() where TEntity : class
        {
            if (!Set<TEntity>().Any())
            {
                var seeds = JsonConvert.DeserializeObject<List<TEntity>>(File.ReadAllText(GetSeedPath<TEntity>()));
                if (typeof(TEntity) == typeof(Student))
                {
                    var counselors = Counselors.ToList();
                    foreach (var student in seeds as List<Student>)
                    { // TODO: 更新吴健雄院逻辑
                        student.CounselorID = counselors.FirstOrDefault(c => c.Department == student.ID.ToStringID().ToDepartment()).ID;
                    }
                }
                AddRange(seeds);
                SaveChanges();
            }
        }

        public void EnsureAllSeeded()
        {
            EnsureSeeded<Counselor>(); // Counselor要先初始化，因为有外键约束
            EnsureSeeded<DeptWuID>(); // 第二个初始化，为了能判定学生是否是吴健雄院的
            EnsureSeeded<Student>();
            EnsureSeeded<Administrator>();
            EnsureSeeded<ChoiceQuestion>();
            EnsureSeeded<TrueFalseQuestion>();

            SaveChanges();
        }

        public static string GetSeedPath<TEntity>() where TEntity : class
        {
            string seedPath = Program.ContentRootPath;
            if (Program.EnvironmentName == "Development")
            {
                seedPath = Path.Combine(seedPath, "HistoryContest.Server", "Data", "Source", "Seeds");
            }
            else
            {
                seedPath = Path.Combine(seedPath, "Source", "Seeds");
            }
            return Path.Combine(seedPath, typeof(TEntity).Name + "s.json");
        }
    }
}
