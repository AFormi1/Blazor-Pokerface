using SQLite;
using System.ComponentModel.DataAnnotations;

namespace Pokerface.Models
{
    public class TableModel
    {
        [PrimaryKey, Unique]
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Name darf nicht leer oder bereits verwendet sein")]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        [Range(2, 10, ErrorMessage = "Die Spieleranzahl muss zwischen 2 und 10 sein")]
        public int MaxUsers { get; set; }

        public int CurrentUsers { get; set; }
    }

}
