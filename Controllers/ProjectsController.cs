﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToDoAndNotes3.Data;
using ToDoAndNotes3.Models;
using ToDoAndNotes3.Models.MainViewModels;

namespace ToDoAndNotes3.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private readonly TdnDbContext _context;
        private readonly UserManager<User> _userManager;

        public ProjectsController(TdnDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Projects/CreatePartial
        public IActionResult CreatePartial(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return PartialView("Projects/_CreatePartial", new Project());
        }

        // POST: Projects/CreatePartial
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePartial(Project project, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                project.UserId = _userManager.GetUserId(User);
                _context.Add(project);
                await _context.SaveChangesAsync();
                return Json(new { success = true, redirectTo = returnUrl });
            }
            return PartialView("Projects/_CreatePartial", project);
        }

        // GET: Projects/EditPartial/5
        public async Task<IActionResult> EditPartial(int? id, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }
            return PartialView("Projects/_EditPartial", project);
        }

        // POST: Projects/EditPartial/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPartial(Project project, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(project);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProjectExists(project.ProjectId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return Json(new { success = true, redirectTo = returnUrl });
            }
            return PartialView("Projects/_EditPartial", project);
        }

        // POST: Projects/SoftDelete/5
        public async Task<IActionResult> SoftDelete(int? id, string? returnUrl = null)
        {
            var project = _context.Projects
                .Include(t => t.Tasks)
                .Include(n => n.Notes)
                .SingleOrDefault(p => p.ProjectId == id);
            if (project != null)
            {
                project.IsDeleted = true;
                foreach (var task in project?.Tasks)
                {
                    task.IsDeleted = true;
                }
                foreach (var note in project?.Notes)
                {
                    note.IsDeleted = true;
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToLocal(returnUrl);
        }

        // GET: Projects/DeletePartial/5
        public async Task<IActionResult> DeletePartial(int? id, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }
            return PartialView("Projects/_DeletePartial", project);
        }

        // POST: Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id, string? returnUrl = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects.FirstOrDefaultAsync(t => t.ProjectId == id);

            if (project == null)
            {
                return NotFound();
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            returnUrl = Url.Action(nameof(HomeController.Main), "Home", new { daysViewName = DaysViewName.Today });
            return Json(new { success = true, redirectTo = returnUrl });
        }

        // POST: Projects/Duplicate/5
        public async Task<IActionResult> Duplicate(int? id, string? returnUrl = null)
        {
            var project = _context.Projects
                .Include(t => t.Tasks).ThenInclude(t => t.TaskLabels).ThenInclude(tl => tl.Label)
                .Include(n => n.Notes).ThenInclude(n => n.NoteLabels).ThenInclude(nl => nl.Label)
                .FirstOrDefault(p => p.ProjectId == id);
            if (project != null)
            {
                Project? copy = ProjectsController.DeepCopy(project);

                if (copy == null)
                {
                    return RedirectToLocal(returnUrl);
                }
                await _context.Projects.AddAsync(copy);
                await _context.SaveChangesAsync();
            }
            // mb handle error somehow
            return RedirectToLocal(returnUrl);
        }

        public static Project? DeepCopy(Project oldProject)
        {
            if (oldProject == null || oldProject.UserId == null)
            {
                return null;
            }

            Project copy = new Project()
            {
                UserId = oldProject.UserId,
                Title = "Copy of " + oldProject.Title
            };

            foreach (var task in oldProject?.Tasks)
            {
                var taskCopy = TasksController.DeepCopy(task);
                if (taskCopy != null)
                {
                    copy?.Tasks?.Add(taskCopy);
                }
            }
            // To do
            //foreach (var note in oldProject?.Notes)
            //{
            //    var noteCopy = NotesController.DeepCopy(note);
            //    if (noteCopy != null)
            //    {
            //        copy?.Notes?.Add(noteCopy);
            //    }
            //}
            return copy;
        }
        private bool ProjectExists(int? id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
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
    }
}
