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
using System.IO;
using OfficeOpenXml;
using Microsoft.AspNetCore.Hosting;

namespace HistoryContest.Server.Controllers.APIs
{
    [Authorize(Roles = "Counselor, Administrator")]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class CounselorController : Controller
    {
        private readonly UnitOfWork unitOfWork;
        private IHostingEnvironment hostingEnvironment;

        public CounselorController(UnitOfWork unitOfWork, IHostingEnvironment hostingEnvironment)
        {
            this.unitOfWork = unitOfWork;
            this.hostingEnvironment = hostingEnvironment;
            unitOfWork.StudentRepository.LoadStudentsFromCounselors();
        }
        
        /// <summary>
        /// ���ص�ǰ����Ա����Ժϵ����ѧ�����������EXCEL��
        /// </summary>
        /// <remarks>
        ///     �޲���
        ///     ��δ����
        /// </remarks>
        /// <returns>Ժϵѧ��EXCEL</returns>
        /// <response code="200">���ر�Ժϵ�÷�EXCEL</response>
        [HttpGet("xlsx")]
        public async Task<IActionResult> Export()
        {
            string sWebRootFolder = hostingEnvironment.WebRootPath;
            string sFileName = "123321.xlsx";

            // TODO : �ĳɴ�session�л�ȡDepartmentID
            var counselorid = int.Parse(HttpContext.Session.GetString("id"));
            var id = (unitOfWork.CounselorRepository.GetByID(counselorid)).Department;

            var datatable=(await unitOfWork.StudentRepository.GetByDepartment(id)).AsQueryable().Select(s => (StudentViewModel)s);
            FileInfo file = new FileInfo(Path.Combine(sWebRootFolder, sFileName));
            using (ExcelPackage package = new ExcelPackage(file))
            {
                // ���worksheet
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("aspnetcore");
                //���ͷ
                worksheet.Cells[1, 1].Value = "ѧ��";
                worksheet.Cells[1, 2].Value = "һ��ͨ��";
                worksheet.Cells[1, 3].Value = "����";
                worksheet.Cells[1, 4].Value = "�Ƿ����";
                worksheet.Cells[1, 5].Value = "�÷�";
                //���ֵ
                int number = 2;
                foreach(var student in datatable)
                {
                    worksheet.Cells[number, 1].Value = student.StudentID;
                    worksheet.Cells[number, 2].Value = student.CardID;
                    worksheet.Cells[number, 3].Value = student.Name;
                    worksheet.Cells[number, 4].Value = student.IsCompleted?"��":"��";
                    worksheet.Cells[number, 5].Value = student.Score;
                    number++;
                }
                
                package.Save();
            }
            return File(sFileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        /// <summary>
        /// ��Ժϵ�����ȡ����ѧ������
        /// </summary>
        /// <remarks>
        ///    �ܾ���ѯ����Ժϵ�÷����飬�ڸ���Ա��ѯ��Ժϵѧ���÷�����ʱʹ��
        /// </remarks>
        /// <returns>Ժϵѧ���÷�</returns>
        /// <response code="200">���ر�Ժϵ�÷�����</response>
        /// <response code="403">��������Ա��ѯ��ͬϵѧ��������</response>
        [HttpGet("scores/all/{id}")]
        [ProducesResponseType(typeof(IEnumerable<StudentViewModel>), 200)]
        public async Task<IActionResult> AllScoresByDepartment(Department id)
        {
            if (!HttpContext.User.IsInRole("Administrator") 
                && id != unitOfWork.CounselorRepository.GetByID(int.Parse(HttpContext.Session.GetString("id"))).Department)
            { // ��������Ա��ѯ��ͬϵѧ��������
                return Forbid();
            }

            return Json((await unitOfWork.StudentRepository.GetByDepartment(id)).AsQueryable().Select(s => (StudentViewModel)s));
        }

        /// <summary>
        /// ��ѧ�Ż�ȡ����ѧ������
        /// </summary>
        /// <remarks>
        ///    ѧ���÷�����JSON��ʽ���£�
        ///    
        ///         {
        ///             "studentID"�� int,
        ///             "cardID": int,
        ///             "Name": string,
        ///             "isCompleted": bool,
        ///             "score": int?
        ///         }
        ///     
        /// </remarks>
        /// <returns>ѧ���÷�</returns>
        /// <response code="200">����ѧ���÷�</response>
        /// <response code="404">id��Ӧѧ��������</response>
        /// <response code="403">��������Ա��ѯ��ͬϵѧ��������</response>
        [HttpGet("scores/single/{id}")]
        [ProducesResponseType(typeof(StudentViewModel), 200)]
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

        /// <summary>
        /// ��ȡȫУ�����ſ�
        /// </summary>
        /// <remarks>
        ///    ȫУ�����ſ�JSON��ʽ���£�
        ///    
        ///         {
        ///             "MaxScore": int,
        ///             "AverageScore": int,
        ///             "ScoreBandCount":
        ///             {
        ///                 "HigherThan90": int,
        ///                 "HigherThan75": int,
        ///                 "HigherThan60": int,
        ///                 "Failed": int
        ///             }
        ///         }
        ///     
        /// ����departmentID�ں��Ϊö������ enum Department{  ���� = 010, ����� = 090}  ���ݺ����Ჹ��
        /// </remarks>
        /// <returns>ȫУ�����ſ�</returns>
        /// <response code="200">����ȫУ�����ſ�</response>
        [HttpGet("scores/summary")]
        [ProducesResponseType(typeof(ScoreSummaryViewModel), 200)]
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

        /// <summary>
        /// ����ԺϵID��ȡ�ſ�
        /// </summary>
        /// <remarks>
        ///    Ժϵ�����ſ�JSON��ʽ���£�
        ///    
        ///     {
        ///         "departmentID": int,
        ///         "CounselorName": string,
        ///         "MaxScore": int,
        ///         "AverageScore": int,
        ///         "ScoreBandCount":
        ///         {
        ///             "HigherThan90": int,
        ///             "HigherThan75": int,
        ///             "HigherThan60": int,
        ///             "Failed": int
        ///         }
        ///     }
        ///     
        ///     
        ///     
        /// ����departmentID�ں��Ϊö������ enum Department{  ���� = 010, ����� = 090}  ���ݺ����Ჹ��
        /// </remarks>
        /// <returns>id��ӦԺϵ�����ſ�</returns>
        /// <response code="200">����id��ӦԺϵ�����ſ�</response>
        /// <response code="404">id��ӦԺϵ������</response>
        [HttpGet("scores/summary/{id}")]
        [ProducesResponseType(typeof(ScoreSummaryViewModel), 200)]
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