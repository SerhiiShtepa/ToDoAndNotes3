using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using ToDoAndNotes3.Authorization;
using ToDoAndNotes3.Data;
using ToDoAndNotes3.Models;
using ToDoAndNotes3.Models.MainViewModels;
using static ToDoAndNotes3.Controllers.ManageController;
using ToDoAndNotes3.Models.ManageViewModels;
using System;

namespace ToDoAndNotes3.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TdnDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly SignInManager<User> _signInManager;

        public HomeController(ILogger<HomeController> logger, TdnDbContext context, UserManager<User> userManager, 
            IAuthorizationService authorizationService, SignInManager<User> signInManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
            _signInManager = signInManager;
        }

        // GET: /Home
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Main), new { daysViewName = DaysViewName.Today });
            }
            return View();
        }

        // GET: /Home/Main
        [HttpGet]
        public async Task<IActionResult> Main(int? projectId = null, DaysViewName? daysViewName = null, string? openModal = null, 
            string? search = null, int? labelId = null, bool isGetPartial = false)
        {
            /*
               TempData["CurrentProjectId"]  => project select value on the Tasks/Notes.CreatePartial
               TempData["DaysViewName"] = daysViewName; => for default date on the Tasks/Notes.CreatePartial
               ViewData["ReturnUrl"] => important for the future partial-update-url generation
               ViewData["DisplayDataTitle"] => just for info about current view
             */

            string? userId = _userManager.GetUserId(User);
            var defaultProject = GetOrCreateDefaultProject();
            //SeedDbData();

            if (daysViewName is not null)
            {
                projectId = null;
                labelId = null;
                ViewData["ReturnUrl"] = Url.Action(nameof(Main), "Home", new { daysViewName });
                TempData["DaysViewName"] = daysViewName;
                ViewData["DisplayDataTitle"] = daysViewName;
                if (daysViewName == DaysViewName.Unsorted)
                {
                    TempData["CurrentProjectId"] = defaultProject.ProjectId;
                }
            }
            if (projectId is not null)
            {
                var isAuthorized = await _authorizationService.AuthorizeAsync(
                                                  User, _context.Projects.Find(projectId), EntityOperations.FullAccess);
                if (!isAuthorized.Succeeded)
                {
                    return Forbid();
                }

                daysViewName = null;
                labelId = null;
                ViewData["ReturnUrl"] = Url.Action(nameof(Main), "Home", new { projectId });
                ViewData["DisplayDataTitle"] = _context.Projects.FirstOrDefault(p => p.ProjectId == projectId).Title;
                TempData["CurrentProjectId"] = projectId;
            }
            if (labelId is not null)
            {
                var isAuthorized = await _authorizationService.AuthorizeAsync(
                                                  User, _context.Labels.Find(labelId), EntityOperations.FullAccess);
                if (!isAuthorized.Succeeded)
                {
                    return Forbid();
                }

                projectId = null;
                daysViewName = null;
                search = null;
                ViewData["ReturnUrl"] = Url.Action(nameof(Main), "Home", new { labelId });
                ViewData["DisplayDataTitle"] = "Label: " + _context.Labels.Find(labelId).Title;
            }           
            if (search is not null)
            {
                ViewData["Search"] = search;
                //ViewData["ReturnUrl"] += $"&search={search}";
                ViewData["ReturnUrl"] = Url.Action(nameof(Main), "Home", new { search });
                ViewData["DisplayDataTitle"] = "Search: " + search;
            }

            if ((TempData.Peek("CurrentProjectId") as int?) is null)
            {
                TempData["CurrentProjectId"] = defaultProject.ProjectId;
            }

            // sort things
            string? dateOrder = TempData.Peek("DateOrder") as string;
            string? hideCompletedString = TempData["HideCompleted"]?.ToString()?.ToLower(); // True => true
            Boolean.TryParse(hideCompletedString, out bool hideCompleted);
            if (dateOrder == null)
            {
                TempData["DateOrder"] = dateOrder = "descending";
            }
            if (hideCompleted == null)
            {
                TempData["HideCompleted"] = hideCompleted = false;
            }

            // check data loading time
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            GeneralViewModel generalViewModel = await LoadGeneralViewModel(userId, daysViewName, projectId, labelId, search);

            stopwatch.Stop();
            TimeSpan elapsedTime = stopwatch.Elapsed;
            Console.WriteLine("Elapsed time: " + elapsedTime);

            SortGeneralViewModel(generalViewModel, dateOrder, hideCompleted);

            if (isGetPartial)
            {
                return PartialView("Home/_MainPartial", generalViewModel);
            }

            return View(generalViewModel);
        }

        // GET: /Home/SidebarPartial
        [HttpGet]
        public IActionResult SidebarPartial()
        {
            return PartialView("_SidebarPartial",
                _context.Projects.Where(p => p.UserId == _userManager.GetUserId(User) && p.IsDefault == false));
        }

        // GET: /Home/Labels
        [HttpGet]
        public async Task<IActionResult> Labels(bool isGetPartial = false)
        {
            ViewData["ReturnUrl"] = Url.Action(nameof(Labels), "Home");

            ProjectLabelViewModel projectLabelViewModel = new ProjectLabelViewModel();
            projectLabelViewModel.Manage = await GetManageViewModelAsync();
            string? userId = _userManager.GetUserId(User);
            var projects = await _context.Projects.Where(p => p.UserId == userId && p.IsDefault == false).ToListAsync();
            var labels = await _context.Labels.Where(p => p.UserId == userId).ToListAsync();
            projectLabelViewModel.Projects = projects;
            projectLabelViewModel.Labels = labels;

            if (isGetPartial)
            {
                return PartialView("Home/_LabelsPartial", projectLabelViewModel.Labels);
            }
            else
            {
                return View(projectLabelViewModel);
            }
        }

        // GET: /Home/RecycleBin
        [HttpGet]
        public async Task<IActionResult> RecycleBin(bool isGetPartial = false)
        {
            ViewData["ReturnUrl"] = Url.Action(nameof(RecycleBin), "Home");

            string userId = _userManager.GetUserId(User);

            // sort things
            string? dateOrder = TempData.Peek("DateOrder") as string;
            string? hideCompletedString = TempData["HideCompleted"]?.ToString()?.ToLower(); // True => true
            Boolean.TryParse(hideCompletedString, out bool hideCompleted);
            if (dateOrder == null)
            {
                TempData["DateOrder"] = dateOrder = "descending";
            }
            if (hideCompleted == null)
            {
                TempData["HideCompleted"] = hideCompleted = false;
            }

            BinViewModel binViewModel = new BinViewModel();
            binViewModel.Manage = await GetManageViewModelAsync();
            // all deleted items
            var projectsInclude = await _context.Projects
                .Where(p => p.UserId == userId)
                .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                .Include(n => n.Notes).ThenInclude(n => n.NoteLabels).ThenInclude(l => l.Label).IgnoreQueryFilters()
                .ToListAsync();

            // ActiveProjects (for sidebar)
            binViewModel.ActiveProjects = _context.Projects.Where(p => p.UserId == userId && p.IsDefault == false).ToList();
            // display all deleted projects
            binViewModel.DeletedProjects = _context.Projects.Where(p => p.UserId == userId && p.IsDefault == false && p.IsDeleted == true).IgnoreQueryFilters().ToList();
            // display deleted tasks & notes (only if its project is active)
            foreach (var project in projectsInclude.Where(p => p.IsDeleted == false))
            {
                binViewModel.TdnElements.AddRange(project.Tasks.Where(t => t.IsDeleted == true));
                binViewModel.TdnElements.AddRange(project.Notes.Where(n => n.IsDeleted == true));
            }
            SortBinViewModel(binViewModel, dateOrder, hideCompleted);

            if (isGetPartial)
            {
                return PartialView("Home/_BinPartial", binViewModel);
            }
            else
            {
                return View(binViewModel);
            }
        }

        // GET: /Home/Manage
        [HttpGet]
        public async Task<IActionResult> Manage(bool isGetPartial = false)
        {
            ViewData["ReturnUrl"] = Url.Action(nameof(Manage), "Home");

            GeneralViewModel generalViewModel = await LoadGeneralViewModel(_userManager.GetUserId(User), daysViewName : DaysViewName.Today);

            if (isGetPartial)
            {
                return PartialView("Home/_ManagePartial", generalViewModel);
            }
            return View(generalViewModel);
        }

        // GET: /Home/Privacy
        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        // POST: /Home/ChangeTempDataValue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeTempDataValue(string? tempDataName, string? tempDataValue = null, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(tempDataName))
            {
                return Json(new { success = false });
            }
            TempData[tempDataName] = tempDataValue;
            return Json(new { success = true });
        }

        // POST: /Home/ChangeTempDataValueNoReload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public void ChangeTempDataValueNoReload(string? tempDataName, string? tempDataValue = null, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(tempDataName))
            {
                return;
            }
            TempData[tempDataName] = tempDataValue;
        }

        // GET: /Home/HardDeleteAllPartial
        [HttpGet]
        public IActionResult HardDeleteAllPartial(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return PartialView("Home/_HardDeleteAllPartial");
        }

        // POST: /Home/HardDeleteAllPartial
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HardDeleteAllPartial(string? returnUrl = null, bool? placeholder = false)
        {
            ViewData["ReturnUrl"] = returnUrl;
            string? userId = _userManager.GetUserId(User);

            // delete projects
            _context.Projects.RemoveRange(_context.Projects
                .Where(p => p.UserId == userId && p.IsDeleted == true)
                .IgnoreQueryFilters());

            // delete tasks and notes
            var activeProjects = _context.Projects
                .Where(p => p.UserId == userId)
                .Include(p => p.Tasks.Where(t => t.IsDeleted == true))
                .Include(p => p.Notes.Where(n => n.IsDeleted == true))
                .IgnoreQueryFilters();

            foreach (var project in activeProjects)
            {
                _context.Tasks.RemoveRange(project.Tasks);
                _context.Notes.RemoveRange(project.Notes);
            }
            _context.SaveChanges();

            return Json(new { success = true });
        }

        #region Helpers
        private Models.Project GetOrCreateDefaultProject()
        {
            var checkDefault = _context.Projects.Where(p => p.UserId == _userManager.GetUserId(User) 
                                                                && p.IsDefault == true).FirstOrDefault();
            if (checkDefault == null)
            {
                var defaultProject = new Models.Project()
                {
                    IsDefault = true,
                    Title = "Unsorted",
                    UserId = _userManager.GetUserId(User),
                };
                _context.Add(defaultProject);
                _context.SaveChanges();
                return defaultProject;
            }
            return checkDefault;
        }
        private async Task<GeneralViewModel> LoadGeneralViewModel(string? userId, DaysViewName? daysViewName = null, int? projectId = null, int? labelId = null, string? search = null)
        {
            GeneralViewModel generalViewModel = new GeneralViewModel();
            generalViewModel.Manage = await GetManageViewModelAsync();
            generalViewModel.Logins = await GetLoginsViewModelAsync();

            if (labelId is not null)
            {
                var projectsLabelInclude = await _context.Projects
                    .Where(p => p.UserId == userId)
                    .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes).ThenInclude(t => t.NoteLabels).ThenInclude(l => l.Label)
                    .ToListAsync();
                generalViewModel.Projects = projectsLabelInclude.Where(p => p.IsDefault == false).ToList(); // do not show default project

                foreach (var project in projectsLabelInclude) // but here using default project
                {
                    generalViewModel.TdnElements.AddRange(project.Tasks.Where(t => t.TaskLabels.Any(tl => tl.Label.LabelId == labelId)));
                    generalViewModel.TdnElements.AddRange(project.Notes.Where(n => n.NoteLabels.Any(nl => nl.Label.LabelId == labelId)));
                }
            }
            else if (search is not null)
            {
                var projectsLabelInclude = await _context.Projects
                    .Where(p => p.UserId == userId)
                    .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes).ThenInclude(t => t.NoteLabels).ThenInclude(l => l.Label)
                    .ToListAsync();
                generalViewModel.Projects = projectsLabelInclude.Where(p => p.IsDefault == false).ToList(); // do not show default project

                foreach (var project in projectsLabelInclude) // but here using default project
                {
                    generalViewModel.TdnElements.AddRange(project.Tasks.Where(t => t.Title.Contains(search, StringComparison.OrdinalIgnoreCase)));
                    generalViewModel.TdnElements.AddRange(project.Notes.Where(n => n.Title.Contains(search, StringComparison.OrdinalIgnoreCase)));
                }
            }
            else if (daysViewName == DaysViewName.Today)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                var projectsTodayInclude = await _context.Projects
                    .Where(p => p.UserId == userId)
                    .Include(t => t.Tasks.Where(t => t.DueDate == today)).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes.Where(n => n.DueDate == today)).ThenInclude(n => n.NoteLabels).ThenInclude(l => l.Label)
                    .ToListAsync();
                generalViewModel.Projects = projectsTodayInclude.Where(p => p.IsDefault == false).ToList();

                foreach (var project in projectsTodayInclude)
                {
                    generalViewModel.TdnElements.AddRange(project.Tasks);
                    generalViewModel.TdnElements.AddRange(project.Notes);
                }
            }
            else if (daysViewName == DaysViewName.Upcoming)
            {
                var projectsUpcomingInclude = await _context.Projects
                    .Where(p => p.UserId == userId)
                    .Include(t => t.Tasks.Where(t => t.DueDate != null)).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes.Where(n => n.DueDate != null)).ThenInclude(t => t.NoteLabels).ThenInclude(l => l.Label)
                    .ToListAsync();
                generalViewModel.Projects = projectsUpcomingInclude.Where(p => p.IsDefault == false).ToList(); // do not show default project

                foreach (var project in projectsUpcomingInclude) // but here using default project
                {
                    generalViewModel.TdnElements.AddRange(project.Tasks);
                    generalViewModel.TdnElements.AddRange(project.Notes);
                }
            }
            else if (daysViewName == DaysViewName.Unsorted)
            {
                var defaultProject = GetOrCreateDefaultProject();

                // 1/2 load projects list separately to save resources
                generalViewModel.Projects = await _context.Projects.Where(p => p.UserId == userId && p.IsDefault == false).ToListAsync();
                // 2/2 load current project content separately to save resources
                var currentProjectInclude = _context.Projects
                    .Where(p => p.UserId == userId && p.ProjectId == defaultProject.ProjectId)
                    .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes).ThenInclude(n => n.NoteLabels).ThenInclude(l => l.Label)
                    .FirstOrDefault();

                generalViewModel.TdnElements.AddRange(currentProjectInclude.Tasks);
                generalViewModel.TdnElements.AddRange(currentProjectInclude.Notes);
            }
            else if (projectId is not null)
            {
                // 1/2 load projects list separately to save resources
                generalViewModel.Projects = await _context.Projects.Where(p => p.UserId == userId && p.IsDefault == false).ToListAsync();
                // 2/2 load current project content separately to save resources
                var currentProjectInclude = _context.Projects
                    .Where(p => p.UserId == userId && p.ProjectId == projectId)
                    .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(l => l.Label)
                    .Include(n => n.Notes).ThenInclude(n => n.NoteLabels).ThenInclude(l => l.Label)
                    .FirstOrDefault();

                generalViewModel.TdnElements.AddRange(currentProjectInclude.Tasks);
                generalViewModel.TdnElements.AddRange(currentProjectInclude.Notes);
            }

            generalViewModel.Labels = await _context.Labels.Where(l => l.UserId == userId).ToListAsync();
            return generalViewModel;
        }
        private async Task<IndexViewModel> GetManageViewModelAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // user general info
                IndexViewModel model = new IndexViewModel
                {
                    Name = user.CustomName,
                    Email = await _userManager.GetEmailAsync(user),
                    HasPassword = await _userManager.HasPasswordAsync(user),
                    Logins = await _userManager.GetLoginsAsync(user),
                };
                return model;
            }
            return null;
        }
        private async Task<ManageLoginsViewModel> GetLoginsViewModelAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // user logins info
                var userLogins = await _userManager.GetLoginsAsync(user);
                var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
                var otherLogins = schemes.Where(auth => userLogins.All(ul => auth.Name != ul.LoginProvider)).ToList();
                ViewData["ShowRemoveButton"] = user.PasswordHash != null || userLogins.Count > 1;

                ManageLoginsViewModel model = new ManageLoginsViewModel()
                {
                    CurrentLogins = userLogins,
                    OtherLogins = otherLogins
                };
                return model;
            }
            return null;
        }
        private void SortGeneralViewModel(GeneralViewModel generalViewModel, string? dateOrder, bool? hideCompleted)
        {
            if (generalViewModel == null)
            {
                return;
            }
            // sort by dateOrder, hideCompleted
            if (dateOrder == "ascending")
            {
                generalViewModel.TdnElements = generalViewModel.TdnElements.OrderBy(t => t.DueDate).ThenBy(t => t.DueTime).ToList();
                generalViewModel.TdnElements = generalViewModel.TdnElements.OrderBy(t => t.DueDate).ThenBy(t => t.DueTime).ToList();
            }
            else
            {
                generalViewModel.TdnElements = generalViewModel.TdnElements.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.DueTime).ToList();
                generalViewModel.TdnElements = generalViewModel.TdnElements.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.DueTime).ToList();
            }
            if (hideCompleted == true)
            {
                generalViewModel.TdnElements = generalViewModel.TdnElements.Where(t => t.IsCompleted == false).ToList();
            }
            else
            {
                generalViewModel.TdnElements = generalViewModel.TdnElements.OrderBy(t => t.IsCompleted).ToList();
            }
        }
        private void SortBinViewModel(BinViewModel binViewModel, string? dateOrder, bool? hideCompleted)
        {
            if (binViewModel == null)
            {
                return;
            }
            // sort by dateOrder, hideCompleted
            if (dateOrder == "ascending")
            {
                binViewModel.TdnElements = binViewModel.TdnElements.OrderBy(t => t.DueDate).ThenBy(t => t.DueTime).ToList();
                binViewModel.TdnElements = binViewModel.TdnElements.OrderBy(t => t.DueDate).ThenBy(t => t.DueTime).ToList();
            }
            else
            {
                binViewModel.TdnElements = binViewModel.TdnElements.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.DueTime).ToList();
                binViewModel.TdnElements = binViewModel.TdnElements.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.DueTime).ToList();
            }
            if (hideCompleted == true)
            {
                binViewModel.TdnElements = binViewModel.TdnElements.Where(t => t.IsCompleted == false).ToList();
            }
        }
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Main), "Home", new { daysViewName = DaysViewName.Today });
            }
        }
        // test
        void SeedDbData()
        {
            if (_context.Projects.Count() > 1)
            {
                return;
            }
            DateOnly RandomDate()
            {
                Random random = new Random();
                // Generate random year between 1900 and 2100
                int year = random.Next(2024, 2026);
                int month = random.Next(1, 13);
                int maxDay = DateTime.DaysInMonth(year, month);
                int day = random.Next(1, maxDay + 1);
                DateOnly randomDate = new DateOnly(year, month, day);
                return randomDate;
            }
            TimeOnly RandomTime()
            {
                Random random = new Random();
                int hour = random.Next(0, 24);
                int minute = random.Next(0, 60);
                int second = random.Next(0, 60);
                int millisecond = random.Next(0, 1000);
                TimeOnly randomTime = new TimeOnly(hour, minute, second, millisecond);
                return randomTime;
            }

            var habbitLabel = new Label()
            {
                Title = "habbit",
                UserId = _userManager.GetUserId(User),
            };
            var importantLabel = new Label()
            {
                Title = "important",
                UserId = _userManager.GetUserId(User),
            };
            var repeatLabel = new Label()
            {
                Title = "repeat",
                UserId = _userManager.GetUserId(User),
            };
            _context.Labels.AddRange(habbitLabel, importantLabel, repeatLabel);

            for (int i = 0; i < 10; i++)
            {
                var project = new Models.Project()
                {
                    UserId = _userManager.GetUserId(User),
                    IsDeleted = false,
                    Title = i + "-Project title lorem",
                };
                
                for (int j = 0; j < 10; j++)
                {
                    Models.Task task = new Models.Task();
                    task.Title = j + "-Task title lorem ";
                    task.Description = j + "-Desc ";
                    task.TaskLabels = new List<TaskLabel>()
                    {
                        new TaskLabel()
                        {
                            Label = importantLabel,
                            Task = task,
                        },
                        new TaskLabel()
                        {
                            Label = repeatLabel,
                            Task = task,
                        },
                    };
                    if (j % 5 == 0)
                    {
                        task.DueDate = DateOnly.FromDateTime(DateTime.Now);  
                    }
                    else
                    {
                        task.DueDate = RandomDate();
                    }
                    task.DueTime = RandomTime();
                    project.Tasks.Add(task);
                }
                for (int j = 0; j < 10; j++)
                {
                    Note note = new Note();
                    note.Title = j + "-Task title lorem ";
                    note.ShortDescription = j + "-Desc ";
                    note.NoteLabels = new List<NoteLabel>()
                    {
                        new NoteLabel()
                        {
                            Label = importantLabel,
                            Note = note,
                        },
                        new NoteLabel()
                        {
                            Label = repeatLabel,
                            Note = note,
                        },
                    };
                    if (j % 5 == 0)
                    {
                        note.DueDate = DateOnly.FromDateTime(DateTime.Now);
                    }
                    else
                    {
                        note.DueDate = RandomDate();
                    }
                    note.DueTime = RandomTime();
                    note.NoteDescription = new NoteDescription()
                    {
                        Description = $"<p>{j}-Hello World!</p>\r\n  <p>Some initial <strong>bold</strong> text</p>\r\n  <p><br /></p>"
                    };
                    project.Notes.Add(note);
                }

                _context.Projects.Add(project);
            }            
            _context.SaveChanges();

            
        }
        #endregion
    }
}
