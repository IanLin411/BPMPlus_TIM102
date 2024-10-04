using BPMPlus.Attributes;
using BPMPlus.Data;
using BPMPlus.Models;
using BPMPlus.Service;
using BPMPlus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Server;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.IO.Compression;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BPMPlus.Controllers
{
	public class FormReviewController : BaseController
	{
		ApplicationDbContext _context;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly EmailService _emailService;
		private readonly FormReview _formReview;

		public FormReviewController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, EmailService emailService, FormReview formReview) : base(context)
		{
			_context = context;
			_webHostEnvironment = webHostEnvironment;
			_emailService = emailService;
			_formReview = formReview;
		}

        // 確認目前登入user 的權限是否有符合 && 確認當前FormRecord的功能為何, 回傳對應結果
        [HttpGet]
		public async Task<JsonResult> CheckEmployeeAuthorize(string id)
		{
			try
			{
                User user = await GetAuthorizedUser();  //驗證User

                // 查最新FormRecord 內容
                var latestDetails = await _context.FormRecord
                                                   .AsNoTracking()
                                                   .Where(c => c.FormId == id && user.UserId == c.UserId)
                                                   .OrderByDescending(t => t.ProcessingRecordId)
                                                   .Select(c => new
                                                   {
                                                       c.ResultId,
                                                       c.UserActivityId,
                                                       c.UserId,
                                                       c.FormId,
                                                   })
                                                   .FirstOrDefaultAsync();

                if (latestDetails == null)
                {
                    return Json(new { status = false });
                }

                // 查詢ProcessNode的這張工單且功能為08的userId
                var processNodeHandler = await _context.ProcessNodes
                    .Where(pn => pn.FormId == id && pn.UserActivityId == "08")
                    .Select(pn => pn.UserId)
                    .FirstOrDefaultAsync();

                // 撈出其processNodeHandler的userName
                var Handler = await _context.User
                    .Where(u => u.UserId == processNodeHandler)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();

                // 查詢工單預估工時
                var manday = await _context.Form
                   .Where(fr => fr.FormId == id)
                   .Select(fr => fr.ManDay)
                   .FirstOrDefaultAsync();

                // User 有以下其中一個權限 &  當前FormRecord的 功能<=6
                if ((user.PermittedTo("02") || user.PermittedTo("03") || user.PermittedTo("04") || user.PermittedTo("05") || user.PermittedTo("06")) && int.Parse(latestDetails.UserActivityId) <= 6)
                {
                    return Json(new { status = true, userPermit = "other" });
                }
                // User為指派方07
                if (user.PermittedTo("07") && latestDetails.UserActivityId == "07")
                {
                    return Json(new { status = true, userPermit = "07", handler = Handler, time = manday });
                }
                // User為處理方08
                else if (user.PermittedTo("08") && latestDetails.UserActivityId == "08")
                {
                    return Json(new { status = true, userPermit = "08", handler = Handler, time = manday });
                }
                // User為驗收方09
                else if (user.PermittedTo("09") && latestDetails.UserActivityId == "09")
                {
                    return Json(new { status = true, userPermit = "09", handler = Handler, time = manday });
                }
                return Json(new { status = false });
            }
			catch (Exception ex)
			{
                Console.WriteLine(ex.Message);
                return Json(new { status = false , message = "系統錯誤, 請洽系統管理員" });
            }
		}

		// 指派方07輸入 指派人員欄位時會檢查是否與指派方同部門以及有處理權限的員工
		[HttpPost]
		public async Task<JsonResult> AssignEmployee(string empName)
		{
			try
			{
                User user = await GetAuthorizedUser();  //驗證User

                // 查詢是否有此員工
                var handler = await _context.User
                    .Where(u => u.UserName == empName)
                    .Select(e => new
                    {
                        userName = e.UserName,
                        userDepartmentId = e.DepartmentId,
                    })
                    .FirstOrDefaultAsync();

                if (handler == null)
                {
                    return Json(new { status = false, message = "找不到此員工" });
                }

                var userWithGroups = await _context.User
                .Include(u => u.PermissionGroups).ThenInclude(pg => pg.Users).FirstOrDefaultAsync(u => u.UserName == empName);

                var userActivityList = userWithGroups.PermissionGroups
                    .Where(u => u.PermissionGroupId == "G0001" || u.PermissionGroupId == "G0006" || u.PermissionGroupId == "G0008")
                    .SelectMany(u => u.Users)
                    .Select(u => u.UserName)
                    .ToList();

                if (handler.userDepartmentId == user.DepartmentId && userActivityList.Contains(empName))
                {
                    return Json(new { status = true, user = handler.userName });
                }
                else return Json(new { status = false });
            }
			catch(Exception ex)
			{
                Console.WriteLine(ex.Message);
                return Json(new { status = false, message = "系統錯誤, 請洽系統管理員" });
            }
		}

		// GET: /FormReview/Index/id
		[HttpGet]
		[AuthAttribute]
		public async Task<ActionResult> Index(string id)
		{
			User user = await GetAuthorizedUser();  //驗證User

			// 確認user 是否為空
			if (user == null)
			{
				return RedirectToAction("login", "Login");
			}
			// 確認id 是否為空
			if (string.IsNullOrEmpty(id))
			{
				return RedirectToAction("Index", "ToDoList");
			}

			try
			{
				// 確認最新一筆工單紀錄是不是審核中RS4 & user是否相同選userId
				var todoReview = await _context.FormRecord
												.AsNoTracking()
												.Where(fr => fr.UserId == user.UserId && fr.ResultId == "RS4")
												.OrderByDescending(d => d.ProcessingRecordId)
												.Select(fr => fr.UserId)
												.FirstOrDefaultAsync();

				// 確認最新一筆工單紀錄是否不為空
				bool lastestToDoReview = todoReview != null;

				// 進該工單頁面
				if (lastestToDoReview)
				{
					// 審核方點進待辦清單=>點選單號=>回傳頁面
					var formViewModel = await GetFormReviewViewModel(id, user.UserId);
					return View(formViewModel);
				}
				return RedirectToAction("Index", "ToDoList");	// 返回待辦清單
			}
			catch (Exception ex)
			{
                Console.WriteLine(ex.Message);
				TempData["AlertMessage"] = "發生未知錯誤，請聯繫系統管理員。";
			}
			return RedirectToAction("Index", "ToDoList");
		}

		// POST :  FormReview/Create
		// 審核方輸入Remark 且點選核准or退回送出後觸發Create Action
		// 帶入Form & 核准or 退回 參數
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(FormReviewViewModel formReviewVM, string reviewResult)
		{
            User user = await GetAuthorizedUser();  //驗證User

            try
			{
				// 查詢該筆工單的最新部門審核者部門Id, UserId
				var latestDetails = await _context.FormRecord
						.AsNoTracking()
						.Where(fr => fr.FormId == formReviewVM.FormId && user.UserId == fr.UserId)
						.OrderByDescending(f => f.ProcessingRecordId)
						.Select(c => new FormRecord
						{
							ProcessingRecordId = c.ProcessingRecordId,
							DepartmentId = c.DepartmentId,
							UserId = c.UserId,
							GradeId = c.GradeId,
							UserActivityId = c.UserActivityId,
							Remark = c.Remark,
						}).FirstOrDefaultAsync();

				// 查詢被指派的員工細項
				var assignEmployee = await _context.User
					  .AsNoTracking()
					  .Where(u => u.UserName == formReviewVM.AssginEmployee)
					  .Select(u => new User
					  {
						  UserId = u.UserId,
						  DepartmentId = u.DepartmentId,
						  GradeId = u.GradeId,
					  })
					  .FirstOrDefaultAsync();

				// 提單及驗收方 UserActivityId 分別是 01 , 09 
				var userActivityBeginAndEnd = new List<string> { "01", "09" };

				// user按下核准送出
				if (reviewResult == "approve")
				{
					// 如果user不等於提單方or 驗收方
					if ( !userActivityBeginAndEnd.Contains(formReviewVM.UserActivityId ))
					{
						// 呼叫創建ProcessingRecordId方法, 給出下2筆FormRecord主鍵ID
						List<string> formRecordIdList = await GetCreateFormRecordIdListAsync(2);

						// 創建當前user核准工單
						var createApproveFormRecord = await _formReview.CreateApproveFormRecord(formReviewVM, new List<string> { formRecordIdList[0] }, latestDetails);

						await _context.AddAsync(createApproveFormRecord);

						// 抓下一筆所需資訊
						var nextDetails = await GetDetails(formReviewVM, user.UserId, latestDetails.UserActivityId, true);

						// 如果當前FormRecord的流程節點是指派方07 & 當前user 有07的權限
						if (formReviewVM.UserActivityId == "07" && user.PermittedTo("07"))
						{
							// 創建由一筆指派方填寫的下一位審核中 FormRecord
							var createNewAssignEmployeeReviewFormRecord = await _formReview.CreateNextReviewFormRecord(formReviewVM, new List<string> { formRecordIdList[1] }, assignEmployee, nextDetails);

							_context.Add(createNewAssignEmployeeReviewFormRecord);

							// 更新ProcessNode Table 的UserId欄位
							await UpdateProcessNodeUserId(formReviewVM, assignEmployee);
						}
						else
						{
                            // 創建預設的下一位審核中 FormRecord
                            var createNextReviewFormRecord = await _formReview.CreateNextReviewFormRecord(formReviewVM, new List<string> { formRecordIdList[1] }, assignEmployee, nextDetails);

							await _context.AddAsync(createNextReviewFormRecord);
						}
						await _context.SaveChangesAsync();

						// 更新Form Table的內容
						await UpdateFormDetails(formReviewVM, assignEmployee, nextDetails, user);

						// 寄信通知下一位User
						await SendNextEmployeeEmail(formReviewVM);
					}

					// 驗收階段
					else
					{
                        // 呼叫創建ProcessingRecordId方法, 給出下2筆FormRecord主鍵ID
                        List<string> formRecordIdList = await GetCreateFormRecordIdListAsync(2);

                        // 創建當前user核准工單
                        var createApproveFormRecord = await _formReview.CreateApproveFormRecord(formReviewVM, new List<string> { formRecordIdList[0] }, latestDetails);

                        // 抓下一筆所需資訊
                        var nextDetails = await GetDetails(formReviewVM, user.UserId, latestDetails.UserActivityId, true);

						// 創建結案FormRecord
						var createFinishFormRecord = await _formReview.CreateFinishFormRecord(formReviewVM, new List<string> { formRecordIdList[1] }, nextDetails);

						await _context.AddAsync(createApproveFormRecord);
						await _context.AddAsync(createFinishFormRecord);
						await _context.SaveChangesAsync();

                        // 更新Form Table的內容
                        await UpdateFormDetails(formReviewVM, assignEmployee, nextDetails, user);
					}

					// 檔案上傳方法
					await UploadFiles(formReviewVM);
					return RedirectToAction("Index", "ToDoList");
				}

				// 退回階段
				else
				{
					// 如果目前該筆工單的FormRecord的功能編號不是第一筆時
					if (latestDetails?.UserActivityId != "01")
					{
                        // 呼叫創建ProcessingRecordId方法, 給出下2筆FormRecord主鍵ID
                        List<string> formRecordIdList = await GetCreateFormRecordIdListAsync(2);

						// 創建退回FormRecord
						var createRejectFormRecord = await _formReview.CreateRejectFormRecord(formReviewVM, new List<string> { formRecordIdList[0] }, latestDetails);

                        // 抓上一筆所需資訊
                        var previousDetails = await GetDetails(formReviewVM, user.UserId, latestDetails.UserActivityId, false);

						// 創建被退回的上一位FormRecord
						var createPreviousReviewFormRecord = await _formReview.CreatePreviousReviewFormRecord(formReviewVM, new List<string> { formRecordIdList[1] }, previousDetails);

						_context.Add(createRejectFormRecord);
						_context.Add(createPreviousReviewFormRecord);
						await _context.SaveChangesAsync();

						await UpdateFormDetails(formReviewVM, assignEmployee, previousDetails, user);

						// 找出formrecord更新完的userId找出其資料
						await SendNextEmployeeEmail(formReviewVM);
					}
					await UploadFiles(formReviewVM);
					return RedirectToAction("Index", "ToDoList");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return BadRequest("系統錯誤, 請洽系統管理員");
			}
		}



        // 更新Form Table的內容
        public async Task UpdateFormDetails(FormReviewViewModel formReviewVM, User assignEmployee, FormReviewViewModel details, User user)
		{
			// 查詢筆工單內容
			var formToUpdate = await _context.Form
							.Where(f => f.FormId == formReviewVM.FormId)
							.FirstOrDefaultAsync();

			if (formToUpdate != null)
			{
                // 更新該工單的 ProcessNodeId, UpdatedTime, ManDay
                formToUpdate.ProcessNodeId = details.ProcessNodeId;
				formToUpdate.UpdatedTime = DateTime.UtcNow;
				//formToUpdate.ManDay = formReviewVM.EstimatedTime;

				// 如果當前user 等於 指派人員
				if (user.UserId == assignEmployee?.UserId)
				{
					if (formReviewVM.EstimatedTime == 0)
					{
						formToUpdate.ManDay = null;
					}
					else
					{
						formToUpdate.ManDay = formReviewVM.EstimatedTime;
					}
					_context.Update(formToUpdate);
				}
				_context.Update(formToUpdate);
				await _context.SaveChangesAsync();
			}
		}

        // 更新ProcessNode Table 的UserId欄位
        public async Task UpdateProcessNodeUserId(FormReviewViewModel formReviewVM, User employee)
		{
			// 查詢該表內容
			var processNodeToUpdate = await _context.ProcessNodes
							  .Where(pn => pn.FormId == formReviewVM.FormId && pn.UserActivityId == "08")
							  .FirstOrDefaultAsync();

			if (processNodeToUpdate != null)
			{
				processNodeToUpdate.UserId = employee.UserId;
			}

			_context.Update(processNodeToUpdate);
			await _context.SaveChangesAsync();
		}

		// 查詢下or 上一位資料
		public async Task<FormReviewViewModel> GetDetails(FormReviewViewModel formReviewVM, string userId, string userActivityId, bool isNext)
		{
			// 抓出該工單流程節點的總長
			var nodeLength = await _context.ProcessNodes
				.Where(f => f.FormId == formReviewVM.FormId)
				.Select(c => new
				{
					c.UserActivityId,
					c.UserId,
					c.DepartmentId,
					c.ProcessNodeId,
				})
				.ToListAsync();

			// 找出目前該工單的流程節點
			var currentDetails = await _context.ProcessNodes
				.Where(f => f.FormId == formReviewVM.FormId && f.UserId == userId && f.UserActivityId == userActivityId)
				.Select(pn => new
				{
					pn.UserActivityId,
					pn.UserId,
					pn.DepartmentId,
					pn.ProcessNodeId,
				})
				.FirstOrDefaultAsync();

			// 判定當前索引位置
			var currentIndex = nodeLength.FindIndex(n =>
				n.UserActivityId == currentDetails.UserActivityId &&
				n.UserId == currentDetails.UserId &&
				n.DepartmentId == currentDetails.DepartmentId &&
				n.ProcessNodeId == currentDetails.ProcessNodeId
			);

			// 由 isNext 參數決定下一個還是上一個
			string targetUserActivity = null;
			string targetUserId = null;
			string targetDepartmentId = null;
			string targetProcessNodeId = null;

			if (currentIndex >= 0 && ((isNext && currentIndex < nodeLength.Count - 1) || (!isNext && currentIndex > 0)))
			{
				var targetIndex = isNext ? nodeLength[currentIndex + 1] : nodeLength[currentIndex - 1];
				targetUserActivity = targetIndex.UserActivityId;
				targetUserId = targetIndex.UserId;
				targetDepartmentId = targetIndex.DepartmentId;
				targetProcessNodeId = targetIndex.ProcessNodeId;
			}

			// 查詢targetUserId 的GradeId
			var targetGradeId = await _context.User.Where(u => u.UserId == targetUserId).Select(s => s.GradeId).FirstOrDefaultAsync();

			return new FormReviewViewModel
			{
				UserActivityId = targetUserActivity,
				UserId = targetUserId,
				DepartmentId = targetDepartmentId,
				ProcessNodeId = targetProcessNodeId,
				GradeId = targetGradeId
			};
		}

		// 寄信給下一位員工
		public async Task SendNextEmployeeEmail(FormReviewViewModel formReviewVM)
		{
			// 找出FormRecord最新userId找出其資料
			var nextEmployeeDetails = await _context.FormRecord
					.AsNoTracking()
					.Include(c => c.User)
					.Where(u => u.FormId == formReviewVM.FormId && u.UserId == u.User.UserId)
					.OrderByDescending(d => d.ProcessingRecordId)
					.Select(e => e.User)
					.FirstOrDefaultAsync();

			_emailService.SendFormReviewEmail(nextEmployeeDetails, formReviewVM.FormId);
		}

		// 檔案上傳
		[RequestSizeLimit(5*1024*1024)] // 5MB
		[RequestFormLimits(MultipartBodyLengthLimit =5*1024*1024)]
		public async Task UploadFiles(FormReviewViewModel fvm)
		{
			try
			{
				var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/upload", fvm.FormId);

				// 檢查資料夾是否存在，如果不存在則創建一個新資料夾
				if (!Directory.Exists(folderPath))
				{
					Directory.CreateDirectory(folderPath);
				}

				// 檢查是否有上傳的檔案
				if (fvm.Files != null && fvm.Files.Count > 0)
				{
					foreach (var file in fvm.Files)
					{
						// 檔案存放的完整路徑
						var filePath = Path.Combine(folderPath, DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd-HHmmss-") + file.FileName);

						// 保存檔案
						using (var stream = new FileStream(filePath, FileMode.Create))
						{
							await file.CopyToAsync(stream);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("檔案上傳失敗: " + ex.Message);
			}
		}
		// 下載檔案
		[HttpPost]
		public IActionResult Download(string id)
		{
			//讀取檔案
			var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "upload");
			filePath = Path.Combine(filePath, id);

			if (!Directory.Exists(filePath))
			{
				return Content("無檔案可下載");
			}

			string[] allFiles = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);

			if (allFiles.Length == 0)
			{
				return Content("無檔案可下載");
			}

			byte[] data = null;
			using (var memoryStream = new MemoryStream())
			{
				using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
				{
					foreach (var file in allFiles)
					{
						archive.CreateEntryFromFile(file, Path.GetFileName(file));
					}
				}
				memoryStream.Seek(0, SeekOrigin.Begin);
				data = memoryStream.ToArray();
			}

			return File(data, "application/zip", "test.zip");

		}

		// 判斷檔案的MIME類型
		private string GetContentType(string fileName)
		{
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			return extension switch
			{
				".pdf" => "application/pdf",
				".jpg" => "image/jpeg",
				".jpeg" => "image/jpeg",
				".png" => "image/png",
				".txt" => "text/plain",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				".xls" => "application/vnd.ms-excel",
				".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				_ => "application/octet-stream", // 預設二進位檔案類型
			};
		}

		// 回傳ViewModel
		private async Task<FormReviewViewModel> GetFormReviewViewModel(string formId, string userId)
		{
			User user = await GetAuthorizedUser();

			// 目前最新進度
			var latestStatus = await _context.FormRecord
				.AsNoTracking()
				.Where(c => c.FormId == formId && userId == c.UserId)
				.OrderByDescending(t => t.ProcessingRecordId)
				.Select(c => new
				{
					resultId = c.ResultId,
					userActivityId = c.UserActivityId,
					userId = c.UserId,
					userName = user.UserName,
				})
				.FirstOrDefaultAsync();

			//顯示在View 欄位
			var formViewModel = await _context.Form
				.AsNoTracking()
				.Where(c => c.FormId == formId)
				.Select(m => new FormReviewViewModel
				{
					FormId = m.FormId,
					UserName = latestStatus.userName,
					UserId = userId,
					Date = m.Date,
					CategoryId = m.CategoryId,
					CategoryDescription = m.Category.CategoryDescription,
					DepartmentId = m.DepartmentId,
					DepartmentName = m.Department.DepartmentName,
					CurrentResults = latestStatus.resultId ?? "Unknown",
					CurrentResultsDescription = _context.Result
						.Where(r => r.ResultId == latestStatus.resultId)
						.Select(r => r.ResultDescription)
						.FirstOrDefault(),
					NeedEmployees = _context.User
						.Where(u => u.UserId == m.UserId)
						.Select(u => u.UserName)
						.FirstOrDefault(),
					HopeFinishDate = m.ExpectedFinishedDay,
					BelongProjects = m.Project.ProjectName,
					EstimatedTime = m.ManDay.HasValue ? (int)m.ManDay : 0,
					Content = m.Content,
					UserActivityId = latestStatus.userActivityId,
					ProcessNodeId = m.ProcessNodeId,
					FormProcessFlow = _context.ProcessNodes.Include(c => c.UserActivity)
					 .Where(c => c.FormId == formId)
					 .AsNoTracking()
					 .AsSplitQuery()
					 .Select(c => new FormReviewProcessFlowViewModel
					 {
						 ProcessNodeId = c.ProcessNodeId,
						 UserActivityId = c.UserActivityId,
						 UserActivityIdDescription = c.UserActivity.UserActivityIdDescription

					 }).ToList(),
					FormRecordList = _context.FormRecord.Where(fr => fr.FormId == formId)
						.Select(fr => new FormReviewFormProcessViewModel
						{
							Date = fr.Date,
							UserActivityDes = fr.UserActivity.UserActivityIdDescription,
							UserName = fr.User.UserName,
							Remark = fr.Remark,
							ResultDes = fr.Result.ResultDescription,
						}).ToList(),
				})
					.FirstOrDefaultAsync();

			//流程進度
			// 該工單的流程節點全部
			var processNode = _context.ProcessNodes.Include(c => c.UserActivity)
				 .Where(c => c.FormId == formId)
				 .AsNoTracking()
				 .AsSplitQuery()
				 .Select(c => new FormReviewProcessFlowViewModel
				 {
					 ProcessNodeId = c.ProcessNodeId,
					 UserActivityId = c.UserActivityId,
					 UserActivityIdDescription = c.UserActivity.UserActivityIdDescription

				 }).ToList();

			if (processNode.Any(c => c.ProcessNodeId == formViewModel.ProcessNodeId)) //Any適用判斷true/false
			{
				var node = processNode.FirstOrDefault(c => c.ProcessNodeId == formViewModel.ProcessNodeId);
				node.IsHightLight = true;
			}
			formViewModel.FormProcessFlow = processNode;

			return formViewModel;
		}
	}
}
