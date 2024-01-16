﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ToDoAndNotes3.Models
{
    public class Task
    {
        public int? TaskId { get; set; }
        public int? ProjectId { get; set; }
        [Required]
        public string? Title { get; set; }
        public string? Description { get; set; } = default!;
        public bool IsCompleted { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        [DataType(DataType.Date)]
        public DateOnly? DueDate { get; set; }
        [DataType(DataType.Time)]
        public TimeOnly? DueTime { get; set; }
        public ICollection<TaskLabel>? TaskLabels { get; set; }
    }
}