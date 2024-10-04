using BPMPlus.Controllers;
using BPMPlus.Data;
using BPMPlus.Models;
using BPMPlus.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPMPlus.Service
{
	public class FormReview
	{
		private readonly ApplicationDbContext _context;

		public FormReview(ApplicationDbContext context)
		{
			_context = context;
		}
        // 創建核准FormRecord
        public async Task<FormRecord> CreateApproveFormRecord(FormReviewViewModel formReviewVM, List<string> formRecordIdList, FormRecord latestDetails)
		{
			var createApproveFormRecord = new FormRecord
			{
				ProcessingRecordId = formRecordIdList.FirstOrDefault(),
				Remark = formReviewVM.Remark,
				FormId = formReviewVM.FormId,
				DepartmentId = latestDetails.DepartmentId,
				UserId = latestDetails.UserId,
				ResultId = "RS2",
				UserActivityId = formReviewVM.UserActivityId,
				GradeId = latestDetails.GradeId,
				Date = DateTime.UtcNow,
				CreatedTime = DateTime.UtcNow,
				UpdatedTime = DateTime.UtcNow,
			};
			return createApproveFormRecord;
		}

        // 創建下一位審核中FormRecord
        public async Task<FormRecord> CreateNextReviewFormRecord(FormReviewViewModel formReviewVM, List<string> formRecordIdList, User employee, FormReviewViewModel nextDetails)
		{
            // 創建指派人員的審核中FormRecord
            if (formReviewVM.UserActivityId == "07")
			{
				var NewAssignEmployeeReviewFormRecord = new FormRecord
				{
					ProcessingRecordId = formRecordIdList.FirstOrDefault(),
					Remark = "",
					FormId = formReviewVM.FormId,
					DepartmentId = employee.DepartmentId,
					UserId = employee.UserId,
					ResultId = "RS4",
					UserActivityId = nextDetails.UserActivityId,
					GradeId = employee.GradeId,
					Date = DateTime.UtcNow,
					CreatedTime = DateTime.UtcNow,
					UpdatedTime = DateTime.UtcNow,
				};
				return NewAssignEmployeeReviewFormRecord;
			}

            // 創建預設的審核中FormRecord
            else
            {
				var NextReviewFormRecord = new FormRecord
				{
					ProcessingRecordId = formRecordIdList.FirstOrDefault(),
					Remark = "",
					FormId = formReviewVM.FormId,
					DepartmentId = nextDetails.DepartmentId,
					UserId = nextDetails.UserId,
					ResultId = "RS4",
					UserActivityId = nextDetails.UserActivityId,
					GradeId = nextDetails.GradeId,
					Date = DateTime.UtcNow,
					CreatedTime = DateTime.UtcNow,
					UpdatedTime = DateTime.UtcNow,
				};
				return NextReviewFormRecord;
			}
		}

        // 創建上一筆審核中FormRecord
        public async Task<FormRecord> CreatePreviousReviewFormRecord(FormReviewViewModel formReviewVM, List<string> formRecordIdList, FormReviewViewModel previousDetails)
		{
			var previousReviewFormRecord = new FormRecord
			{
				ProcessingRecordId = formRecordIdList.FirstOrDefault(),
				Remark = "",
				FormId = formReviewVM.FormId,
				DepartmentId = previousDetails.DepartmentId,
				UserId = previousDetails.UserId,
				ResultId = "RS4",
				UserActivityId = previousDetails.UserActivityId,
				GradeId = previousDetails.GradeId,
				Date = DateTime.UtcNow,
				CreatedTime = DateTime.UtcNow,
				UpdatedTime = DateTime.UtcNow,
			};

			return previousReviewFormRecord;
		}

        // 創建退回FormRecord
        public async Task<FormRecord> CreateRejectFormRecord(FormReviewViewModel formReviewVM, List<string> formRecordIdList, FormRecord latestDetails)
		{
			var rejectFormRecord = new FormRecord
			{
				ProcessingRecordId = formRecordIdList.FirstOrDefault(),
				Remark = formReviewVM.Remark,
				FormId = formReviewVM.FormId,
				DepartmentId = latestDetails.DepartmentId,
				UserId = latestDetails.UserId,
				ResultId = "RS1",
				UserActivityId = latestDetails.UserActivityId,
				GradeId = latestDetails.GradeId,
				Date = DateTime.UtcNow,
				CreatedTime = DateTime.UtcNow,
				UpdatedTime = DateTime.UtcNow,
			};
			return rejectFormRecord;
		}

        // 創建結案FormRecord
        public async Task<FormRecord> CreateFinishFormRecord(FormReviewViewModel formReviewVM, List<string> formRecordIdList, FormReviewViewModel nextDetails)
		{
			var FinishFormRecord = new FormRecord
			{
				ProcessingRecordId = formRecordIdList.FirstOrDefault(),
				Remark = "",
				FormId = formReviewVM.FormId,
				DepartmentId = nextDetails.DepartmentId,
				UserId = nextDetails.UserId,
				ResultId = "RS3",
				UserActivityId = nextDetails.UserActivityId,
				GradeId = nextDetails.GradeId,
				Date = DateTime.UtcNow,
				CreatedTime = DateTime.UtcNow,
				UpdatedTime = DateTime.UtcNow,
			};
			return FinishFormRecord;
		}
	}
}
