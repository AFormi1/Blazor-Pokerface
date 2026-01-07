using SQLite;
using System.ComponentModel.DataAnnotations;

namespace Pokerface.Models
{
    public class TableModel
    {
        [PrimaryKey, Unique]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name darf nicht leer oder bereits verwendet sein")]
        public string Name { get; set; } = string.Empty;


        [Ignore]
        public int MaxUsers { get; private set; } = 8 ;

        public int CurrentPlayers { get; set; }


    }


}
