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
        /// ��ȡ����Ա����Ժϵ
        /// </summary>
        /// <remarks>
        /// ����Session�д洢��Ժϵ���š�
        /// </remarks>
        /// <returns>����Ա��ӦԺϵ����</returns>
        /// <response code="200">���ظ���Ա���ڵ�Ժϵ����</response>
        /// <response code="404">Session��δ����Ժϵ����</response>
        [HttpGet("Department")]
        [ProducesResponseType(typeof(Department), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetDepartment()
        {
            var id = HttpContext.Session.GetInt32("department");
            if (id == null)
            {
                return NotFound();
            }
            return Json((Department)id);
        }

        /// <summary>
        /// ��ȡһ��Ժϵ����ѧ����ѧ��
        /// </summary>
        /// <remarks>
        /// ���API������Ա��Ӧ������ѧ����ѧ�ŷ��أ���ǰ�˸���ѧ��һ�����ؼ���ѧ����Ϣ���������첽����������������
        /// 
        /// ID�����ǿ�ѡ�ġ����������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID��
        /// </remarks>
        /// <param name="id">Ժϵ����ö��������ѡ��</param>
        /// <returns>Ժϵ����ѧ��ѧ��</returns>
        /// <response code="200">���ظ���Ա��Ӧ������ѧ��ѧ�Ź��ɵ�����</response>
        /// <response code="400">��ǰ�û����Ǹ���Ա���ӦSession��û��Ժϵ����</response>
        /// <response code="403">����Ա��ѯ�Ǳ�ϵ����</response>
        /// <response code="404">ID�������κ�һ��Ժϵ����</response>
        [HttpGet("AllStudents/{id?}")]
        [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AllStudentIDs(Department? id)
        {
            if (id == null && HttpContext.User.IsInRole("Counselor") && HttpContext.Session.Get("department") != null)
            { // ���������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID
                id = (Department)HttpContext.Session.GetInt32("department");
            }
            else
            {
                return BadRequest("Empty argument request invalid");
            }

            if (!HttpContext.User.IsInRole("Administrator") && id != (Department)HttpContext.Session.GetInt32("department"))
            { // ��������Ա��ѯ��ͬϵѧ��������
                return Forbid();
            }

            var students = (await unitOfWork.StudentRepository.GetByDepartment((Department)id));
            if (students == null)
            {
                return NotFound();
            }

            return Json(students.AsQueryable().Select(s => s.ID));
        }

        #region Scores APIs
        /// <summary>
        /// ��ȡһ��Ժϵ����ѧ���ļ�Ҫ�÷���Ϣ
        /// </summary>
        /// <remarks>
        /// ID�����ǿ�ѡ�ġ����������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID��
        /// </remarks>
        /// <param name="id">Ժϵ����ö��������ѡ��</param>
        /// <returns>Ժϵ����ѧ���÷�</returns>
        /// <response code="200">���ر�Ժϵ����ѧ����Ҫ�÷���Ϣ</response>
        /// <response code="400">��ǰ�û����Ǹ���Ա���ӦSession��û��Ժϵ����</response>
        /// <response code="403">����Ա��ѯ�Ǳ�ϵ����</response>
        /// <response code="404">ID�������κ�һ��Ժϵ����</response>
        [HttpGet("Scores/All/{id?}")]
        [ProducesResponseType(typeof(IEnumerable<StudentViewModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AllScoresByDepartment(Department? id)
        {
            if (id == null && HttpContext.User.IsInRole("Counselor") && HttpContext.Session.Get("department") != null)
            { // ���������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID
                id = (Department)HttpContext.Session.GetInt32("department");
            }
            else
            {
                return BadRequest("Empty argument request invalid");
            }

            if (!HttpContext.User.IsInRole("Administrator") && id != (Department)HttpContext.Session.GetInt32("department"))
            { // ��������Ա��ѯ��ͬϵѧ��������
                return Forbid();
            }

            var students = (await unitOfWork.StudentRepository.GetByDepartment((Department)id));
            if (students == null)
            {
                return NotFound();
            }

            return Json(students.AsQueryable().Select(s => (StudentViewModel)s));
        }

        /// <summary>
        /// ��ȡһ��ѧ���ļ�Ҫ�÷���Ϣ
        /// </summary>
        /// <remarks>
        /// ���API��Ҫ����� `POST api/Counselor/AllStudents/{id}` ʹ�ã�ʹǰ���ܹ��Ȼ��ѧ�ţ�Ȼ�����ѧ�ŷ����ִεؼ���ѧ����Ϣ��
        /// </remarks>
        /// <returns>ѧ����Ҫ�÷���Ϣ</returns>
        /// <param name="id">ѧ����ѧ��</param>
        /// <response code="200">����ѧ�Ŷ�Ӧѧ���ĵ÷���Ϣ</response>
        /// <response code="403">����Ա��ѯ�Ǳ�ϵѧ��������</response>
        /// <response code="404">IDû�ж�Ӧ��ѧ��</response>
        [HttpGet("Scores/Single/{id}")]
        [ProducesResponseType(typeof(StudentViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        #endregion

        #region Score Summary APIs
        /// <summary>
        /// ��ȡȫУ�����ſ�
        /// </summary>
        /// <remarks>
        /// **NOTE:�������Ŀǰ��δ����ȷʵ�֣������ڲο�JSON����ֵ**
        /// </remarks>
        /// <returns>ȫУ�����ſ�</returns>
        /// <response code="200">
        /// ����ȫУ�ķ����ſ���ѧ��������
        /// * ������ݲ�����ÿ������ʱ�¼�����õģ�����ÿ��һ��ʱ�䣨��10���ӣ��Զ�����һ��
        /// * ��ˣ�JSON�ļ�������һ��������ʱ�䡱�ļ�¼��
        /// </response>
        [HttpGet("Scores/Summary")]
        [ProducesResponseType(typeof(ScoreSummaryOfSchoolViewModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> ScoreSummaryOfSchool()
        {
            // TODO: ȫУ��Score Summary����/���뻺��, ����ȷʵ�ֻ�ȡȫУ�ſ�
            var model = new ScoreSummaryOfSchoolViewModel
            {
                MaxScore = await unitOfWork.StudentRepository.HighestScore(),
                AverageScore = await unitOfWork.StudentRepository.AverageScore(),
                ScoreBandCount =
                {
                    HigherThan90 = await unitOfWork.StudentRepository.ScoreHigherThan(90),
                    HigherThan75 = await unitOfWork.StudentRepository.ScoreHigherThan(75),
                    HigherThan60 = await unitOfWork.StudentRepository.ScoreHigherThan(60)
                },
                UpdateTime = DateTime.Now
            };
            model.ScoreBandCount.Failed = await unitOfWork.StudentRepository.SizeAsync() - model.ScoreBandCount.HigherThan60;
            return Json(model);
        }

        /// <summary>
        /// ��ȡһ��Ժϵ�ķ����ſ�
        /// </summary>
        /// <remarks>
        /// Ժϵ�����ں��Ϊһ��ö�٣�Ŀǰ�������£�
        ///     
        ///     enum Department
        ///     {
        ///         ���� = 0x010,
        ///         ����� = 0x090
        ///     }
        /// 
        /// ǰ��Ҳ�ɽ���һ����ͬ��ö�ٱ�������ɲ�������ÿ��ö��ֵ���������ݡ�
        /// 
        /// ID�����ǿ�ѡ�ġ����������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID��
        /// </remarks>
        /// <param name="id">Ժϵ����ö��������ѡ��</param>
        /// <returns>ID��ӦԺϵ�ķ����ſ�</returns>
        /// <response code="200">����Ժϵ�����ӦԺϵ�ķ����ſ�</response>
        /// <response code="400">��ǰ�û����Ǹ���Ա���ӦSession��û��Ժϵ����</response>
        /// <response code="404">IDû�ж�Ӧ��Ժϵ</response>
        [HttpGet("Scores/Summary/{id?}")]
        [ProducesResponseType(typeof(ScoreSummaryByDepartmentViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ScoreSummaryByDepartment(Department? id)
        {
            if (id == null && HttpContext.User.IsInRole("Counselor") && HttpContext.Session.Get("department") != null)
            { // ���������ID���ҵ�ǰ�û���֤Ϊ����Ա����ȡSession�е�Ժϵ������ΪID
                id = (Department)HttpContext.Session.GetInt32("department");
            }
            else
            {
                return BadRequest("Empty argument request invalid");
            }

            var counselor = await unitOfWork.CounselorRepository.FirstOrDefaultAsync(c => c.Department == id);
            if (counselor == null)
            {
                return NotFound();
            }

            // TODO: Score Summary���뻺��
            var model = new ScoreSummaryByDepartmentViewModel
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
        #endregion
    }
}
