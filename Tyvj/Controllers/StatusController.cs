﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Tyvj.DataModels;
using Tyvj.ViewModels;

namespace Tyvj.Controllers
{
    public class StatusController : BaseController
    {
        // GET: Status
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetStatuses(int? Page, string Username, int? Result, int? ContestID, int? ProblemID)
        {
            var _statuses = from s in DbContext.Statuses select s;
            if (!string.IsNullOrEmpty(Username))
                _statuses = _statuses.Where(x => x.User.Username.Contains(Username));
            if (Result != null)
            {
                _statuses = _statuses.Where(x => x.ResultAsInt == Result.Value);
            }
            if (ContestID != null)
                _statuses = _statuses.Where(x => x.ContestID == ContestID);
            if (ProblemID != null)
                _statuses = _statuses.Where(x => x.ProblemID == ProblemID);
            _statuses = _statuses.OrderByDescending(x => x.Time);
            var statuses = new List<vStatus>();
            foreach (var status in _statuses.Skip(10 * Page.Value).Take(10).ToList())
            {
                var user = ViewBag.CurrentUser == null ? new User() : (User)ViewBag.CurrentUser;
                var contest = status.Contest;
                if (contest == null || DateTime.Now >= contest.End || user.Role >= UserRole.Master || contest.UserID == user.ID)
                {
                    statuses.Add(new vStatus(status));
                    continue;
                }
                if (contest.Format == ContestFormat.ACM)
                {
                    status.JudgeTasks = new List<JudgeTask>
                    { 
                        new JudgeTask
                        {
                            Result = status.Result,
                            MemoryUsage = status.JudgeTasks.Max(x=>x.MemoryUsage),
                            TimeUsage = status.JudgeTasks.Max(x=>x.TimeUsage),
                            Hint="比赛期间不提供详细信息",
                            StatusID = status.ID
                        }
                    };
                }
                else if (contest.Format == ContestFormat.OI)
                {
                    status.Result = JudgeResult.Hidden;
                    status.JudgeTasks = new List<JudgeTask>
                    { 
                        new JudgeTask
                        {
                            Result = JudgeResult.Hidden,
                            MemoryUsage = status.JudgeTasks.Max(x=>x.MemoryUsage),
                            TimeUsage = status.JudgeTasks.Max(x=>x.TimeUsage),
                            Hint="比赛期间不提供详细信息",
                            StatusID = status.ID
                        }
                    };
                }
                else
                {
                    foreach (var jt in status.JudgeTasks)
                        jt.Hint = "比赛期间不提供详细信息";
                }
                statuses.Add(new vStatus(status));
            }
            return Json(statuses, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(int problem_id, string code, int language_id, int? contest_id)
        {
            var problem = DbContext.Problems.Find(problem_id);
            var user = (User)ViewBag.CurrentUser;
            if (problem == null)
                return Content("Problem not existed");
            Contest contest = new Contest();
            if (contest_id != null)
            {
                contest = DbContext.Contests.Find(contest_id.Value);
                if (!Helpers.Contest.UserInContest(user.ID, contest_id.Value))
                    return Content("Insufficient permissions");
                ContestFormat[] SubmitAnyTime = { ContestFormat.ACM, ContestFormat.OI };
                if (DateTime.Now < contest.Begin && user.Role < UserRole.Master && contest.UserID != user.ID)
                {
                    return Content("Insufficient permissions");
                }
                if (contest.Format == ContestFormat.Codeforces && (from l in DbContext.Locks where l.ProblemID == problem_id && user.ID == l.UserID && l.ContestID == contest_id.Value select l).Count() > 0 && DateTime.Now < contest.End)
                {
                    return Content("Locked");
                }
            }

            var status = new Status
            {
                Code = code,
                LanguageAsInt = language_id,
                ProblemID = problem_id,
                UserID = user.ID,
                Time = DateTime.Now,
                Result = JudgeResult.Pending
            };
            DbContext.Statuses.Add(status);
            var testcase_ids = (from tc in problem.TestCases
                                where tc.Type != TestCaseType.Sample
                                orderby tc.Type ascending
                                select tc.ID).ToList();
            if (contest_id != null)
            {
                if (DateTime.Now < contest.Begin || DateTime.Now >= contest.End || contest.Format == ContestFormat.ACM || contest.Format == ContestFormat.OI)
                {
                    testcase_ids = (from tc in problem.TestCases
                                    where tc.Type != TestCaseType.Sample
                                    orderby tc.Type ascending
                                    select tc.ID).ToList();
                }
                else if (contest.Format == ContestFormat.Codeforces)
                {
                    testcase_ids = (from tc in problem.TestCases
                                    where tc.Type == TestCaseType.Unilateralism
                                    orderby tc.Type ascending
                                    select tc.ID).ToList();
                    var statuses = (from s in DbContext.Statuses
                                    where s.ContestID == contest_id.Value
                                    && s.UserID == user.ID
                                    select s).ToList();
                    foreach (var s in statuses)
                    {
                        if (s.JudgeTasks == null) continue;
                        foreach (var jt in s.JudgeTasks)
                        {
                            testcase_ids.Add(jt.TestCaseID);
                        }
                    }
                    testcase_ids = testcase_ids.Distinct().ToList();
                }

                foreach (var id in testcase_ids)
                {
                    DbContext.JudgeTasks.Add(new JudgeTask
                    {
                        StatusID = status.ID,
                        TestCaseID = id,
                        Result = JudgeResult.Pending,
                        MemoryUsage = 0,
                        TimeUsage = 0,
                        Hint = ""
                    });
                }
                //foreach (var jt in status.JudgeTasks)
                //{
                //    try
                //    {
                //        var group = SignalR.JudgeHub.GetNode();
                //        if (group == null) return Content("No Online Judger");
                //        SignalR.JudgeHub.context.Clients.Group(group).Judge(new Judge.Models.JudgeTask(jt));
                //        SignalR.JudgeHub.ThreadBusy(group);
                //        jt.Result = Entity.JudgeResult.Running;
                //        DbContext.SaveChanges();
                //    }
                //    catch { }
                //}
                //SignalR.CodeCombHub.context.Clients.All.onStatusCreated(new Models.View.Status(status));//推送新状态
                //if (contest.Format == Entity.ContestFormat.OI && DateTime.Now >= contest.Begin && DateTime.Now < contest.End)
                //    return Content("OI");

            }
            DbContext.SaveChanges();
            SignalR.UserHub.context.Clients.All.onStatusChanged(new vStatus(status));
            return Content(status.ID.ToString());
        }
    }
}