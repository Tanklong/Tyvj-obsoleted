﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Tyvj.DataModels;
using Tyvj.ViewModels;

namespace Tyvj.Controllers
{
    public class ContestController : BaseController
    {
        // GET: Contest
        public ActionResult Index()
        {
            var _contests = (from c in DbContext.Contests
                                              where DateTime.Now >= c.Begin && DateTime.Now < c.End
                                              orderby c.End ascending
                                              select c).ToList();
            var contests = new List<vContest>();
            foreach (var c in _contests)
                contests.Add(new vContest(c));
            return View(contests);
        }

        [HttpGet]
        public ActionResult GetContests(int? Page, string Title, int? Format)
        { 
            if(Title == null)Title = "";
            if(Page == null)Page = 0;
            IEnumerable<Contest> _contests = (from c in DbContext.Contests
                                where c.Title.Contains(Title)
                                && !(DateTime.Now >= c.Begin && DateTime.Now <c.End)
                                select c);
            if (Format != null)
                _contests = _contests.Where(x => x.FormatAsInt == Format.Value).OrderByDescending(x=>x.End).Skip(10 * Page.Value).Take(10).ToList();
            else _contests = _contests.OrderByDescending(x => x.End).Skip(10 * Page.Value).Take(10).ToList();
            var contests = new List<vContest>();
            foreach (var c in _contests)
                contests.Add(new vContest(c));
            return Json(contests, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Show(int id)
        { 
            var contest = DbContext.Contests.Find(id);
            return View(contest);
        }

        public ActionResult Standings(int id)
        {
            var contest = DbContext.Contests.Find(id);
            var user = ViewBag.CurrentUser == null ? new User() : (User)ViewBag.CurrentUser;
            if (!Helpers.Contest.UserInContest(user.ID,id))
                return RedirectToAction("Register", "Contest", new { id = id });
            if (contest.Format == ContestFormat.OI && DateTime.Now < contest.End && !ViewBag.IsMaster)
                return RedirectToAction("Message", "Shared", new { msg = "目前不提供比赛排名显示。" });
            ViewBag.AllowHack = false;
            if (User.Identity.IsAuthenticated)
            {
                if (contest.Format == ContestFormat.Codeforces && DateTime.Now >= contest.Begin && DateTime.Now < contest.End)
                    ViewBag.AllowHack = true;
            }
            return View(contest);
        }

        [HttpGet]
        public ActionResult GetStandings(int id)
        {
            var user = ViewBag.CurrentUser == null ? new User() : (User)ViewBag.CurrentUser;
            var contest = DbContext.Contests.Find(id);
            if (!Helpers.Contest.UserInContest(user.ID, id))
                return RedirectToAction("Register", "Contest", new { id = id });
            if (contest.Format == ContestFormat.OI && DateTime.Now < contest.End && !ViewBag.IsMaster)
                return Json(null, JsonRequestBehavior.AllowGet);
            var standings = Helpers.Standings.Build(id);
            return Json(standings, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        public ActionResult Register(int id)
        {
            var contest = DbContext.Contests.Find(id);
            return View(contest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult Register(int id, string Password)
        {
            var contest = DbContext.Contests.Find(id);
            if (string.IsNullOrEmpty(contest.Password) || contest.Password == Password)
            {
                DbContext.ContestRegisters.Add(new ContestRegister
                {
                    ContestID = id,
                    UserID = ViewBag.CurrentUser.ID
                });
                DbContext.SaveChanges();
                return RedirectToAction("Show", "Contest", new { id = id });
            }
            else
            {
                return RedirectToAction("Message", "Shared", new { msg="参赛密码不正确！" });
            }
        }

        public ActionResult Statistics(int id)
        {
            var contest = DbContext.Contests.Find(id);
            var user = ViewBag.CurrentUser == null ? new User() : (User)ViewBag.CurrentUser;
            if (!Helpers.Contest.UserInContest(user.ID, id))
                return RedirectToAction("Register", "Contest", new { id = id });
            var statistics = new int[contest.ContestProblems.Count, 9];
            var i = 0;
            foreach (var p in contest.ContestProblems.OrderBy(x => x.Point))
            {
                var statuses = Helpers.Contest.GetStatuses(p.ProblemID, id);
                if (contest.Format == ContestFormat.OI)
                {
                    var user_ids = (from s in statuses
                                    select s.UserID).Distinct();
                    foreach (var uid in user_ids)
                    {
                        var last_status = statuses.Where(x => x.UserID == uid).LastOrDefault();
                        if (last_status != null)
                        {
                            if (last_status.ResultAsInt < 8)
                                statistics[i, last_status.ResultAsInt]++;
                            else
                                statistics[i, 8]++;
                        }
                    }
                }
                else
                {
                    statistics[i, 0] = statuses.Where(x => x.Result == JudgeResult.Accepted).Count();
                    statistics[i, 1] = statuses.Where(x => x.Result == JudgeResult.PresentationError).Count();
                    statistics[i, 2] = statuses.Where(x => x.Result == JudgeResult.WrongAnswer).Count();
                    statistics[i, 3] = statuses.Where(x => x.Result == JudgeResult.OutputLimitExceeded).Count();
                    statistics[i, 4] = statuses.Where(x => x.Result == JudgeResult.TimeLimitExceeded).Count();
                    statistics[i, 5] = statuses.Where(x => x.Result == JudgeResult.MemoryLimitExceeded).Count();
                    statistics[i, 6] = statuses.Where(x => x.Result == JudgeResult.RuntimeError).Count();
                    statistics[i, 7] = statuses.Where(x => x.Result == JudgeResult.CompileError).Count();
                    statistics[i, 8] = statuses.Where(x => x.Result == JudgeResult.Hacked).Count();
                }
                i++;
            }
            ViewBag.Statistics = statistics;
            return View(contest);
        }
    }
}