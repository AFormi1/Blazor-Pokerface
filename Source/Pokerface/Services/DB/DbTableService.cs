
using Pokerface.Models;

namespace Pokerface.Services.DB
{
    public class DbTableService
    {
        public BaseDataBase DataBase { get; set; } = new BaseDataBase("Table.db");

        public async Task Init()
        {
            await DataBase.Init<TableModel>();
        }
            
        public async Task<List<TableModel>> GetItemsAsync() => await DataBase.GetItemsAsync<TableModel>();

        public async Task<TableModel?> GetItemByIdAsync(Guid id)
        {
            return await DataBase.GetItemByPredicateAsync<TableModel>(u => u.Id == id);
        }

        public async Task<int> SaveItemAsync(TableModel item) => await DataBase.SaveItemAsync(item, x => x.Id == item.Id);

        public async Task<int> DeleteItemAsync(TableModel item) => await DataBase.DeleteItemAsync<TableModel>(x => x.Id == item.Id);

    }
}
