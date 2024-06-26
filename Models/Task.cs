﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ToDoAndNotes3.Models
{
    public class Task : TdnSortElement
    {
        public int? TaskId { get; set; }
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        [Required]
        [MaxLength(150)]
        public string? Title { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; } = default!;
        override public bool IsCompleted { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        [DataType(DataType.Date)]
        override public DateOnly? DueDate { get; set; }
        [DataType(DataType.Time)]
        override public TimeOnly? DueTime { get; set; }
        public ICollection<TaskLabel>? TaskLabels { get; set; }
    }
}
