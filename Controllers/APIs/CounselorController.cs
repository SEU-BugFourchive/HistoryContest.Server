using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HistoryContest.Server.Models;
using HistoryContest.Server.Models.ViewModels;
using HistoryContest.Server.Data;

namespace HistoryContest.Server.Controllers.APIs
{
    [Authorize(Roles = "Counselor, Administrator")]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class CounselorController : Controller
    {
        private readonly UnitOfWork unitOfWork;

        public CounselorController(UnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            unitOfWork.StudentRepository.LoadStudentsFromCounselors();
        }

        // ��Ժϵ�����ȡ����ѧ������
        [HttpGet("scores/all/{id}")]
        public async Task<IActionResult> AllScoresByDepartment(Department id)
        {
            if (!HttpContext.User.IsInRole("Administrator") 
                && 
                id != unitOfWork.CounselorRepository.GetByID(int.Parse(HttpContext.Session.GetString("id"))).Department)
            { // ��������Ա��ѯ��ͬϵѧ��������
                return Forbid();
            }

            return Json((await unitOfWork.StudentRepository.GetByDepartment(id)).AsQueryable().Select(s => (StudentViewModel)s));
        }

        // ��ѧ�Ż�ȡ����ѧ������
        [HttpGet("scores/single/{id}")]
        public async Task<IActionResult> StudentScoreById(int id)
        {
            var student = await unitOfWork.StudentRepository.GetByIDAsync(id);
            if (student == null)
            {
                return NotFound();
            }

            if (!HttpContext.User.IsInRole("Administrator") && student.CounselorID != int.Parse(HttpContext.Session.GetString("id")))
            { // ��������Ա��ѯ��ͬϵѧ��������
                return Forbid();
            }

            return Json((StudentViewModel)student);
        }

        // ��ȡȫУ�����ſ�
        [HttpGet("scores/summary")]
        public async Task<IActionResult> ScoreSummaryOfSchool()
        {
            // TODO: Score Summary���뻺��
            var model = new ScoreSummaryViewModel
            {
                MaxScore = await unitOfWork.StudentRepository.HighestScore(),
                AverageScore = await unitOfWork.StudentRepository.AverageScore(),
                ScoreBandCount =
                {
                    HigherThan90 = await unitOfWork.StudentRepository.ScoreHigherThan(90),
                    HigherThan75 = await unitOfWork.StudentRepository.ScoreHigherThan(75),
                    HigherThan60 = await unitOfWork.StudentRepository.ScoreHigherThan(60)
                }
            };
            model.ScoreBandCount.Failed = await unitOfWork.StudentRepository.SizeAsync() - model.ScoreBandCount.HigherThan60;
            return Json(model);
        }

        // ����ԺϵID��ȡ�ſ�
        [HttpGet("scores/summary/{id}")]
        public async Task<IActionResult> ScoreSummaryByDepartment(Department id)
        {
            var counselor = await unitOfWork.CounselorRepository.FirstOrDefaultAsync(c => c.Department == id);
            if (counselor == null)
            {
                return NotFound();
            }

            // TODO: Score Summary���뻺��
            var model = new ScoreSummaryViewModel
            {
                DepartmentID = counselor.Department,
                CounselorName = counselor.Name,
                MaxScore = await unitOfWork.StudentRepository.HighestScoreByDepartment(counselor.Department),
                AverageScore = await unitOfWork.StudentRepository.AverageScoreByDepartment(counselor.Department),
                ScoreBandCount =
                {
                    HigherThan90 = await unitOfWork.StudentRepository.ScoreHigherThanByDepartment(90, counselor.Department),
                    HigherThan75 = await unitOfWork.StudentRepository.ScoreHigherThanByDepartment(75, counselor.Department),
                    HigherThan60 = await unitOfWork.StudentRepository.ScoreHigherThanByDepartment(60, counselor.Department)
                }
            };
            model.ScoreBandCount.Failed = await unitOfWork.StudentRepository.SizeByDepartment(counselor.Department) - model.ScoreBandCount.HigherThan60;
            return Json(model);
        }
    }
}