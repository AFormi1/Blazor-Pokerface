
using Pokerface.Models;

namespace Pokerface.Services.DB
{
    public class DbTableService
    {
        public BaseDataBase DataBase { get; set; } = new BaseDataBase("Table.db");

        public async Task Init()
        {
            await DataBase.Init<TableModel>();

            //if this task runs, we can be shure that we have no gamesessions and no current users, so reset the values
            var tables = await GetItemsAsync();
            foreach (var table in tables)
            {
                table.CurrentUsers = 0;
                await SaveItemAsync(table);
            }
        }

        public async Task<List<TableModel>> GetItemsAsync() => await DataBase.GetItemsAsync<TableModel>();

        public async Task<TableModel?> GetItemByIdAsync(int id)
        {
            return await DataBase.GetItemByPredicateAsync<TableModel>(u => u.Id == id);
        }

        public async Task<int> SaveItemAsync(TableModel item) => await DataBase.SaveItemAsync(item, x => x.Id == item.Id);

        public async Task<int> DeleteItemAsync(TableModel item) => await DataBase.DeleteItemAsync<TableModel>(x => x.Id == item.Id);
        public async Task<int> GetCountAsync() => await DataBase.GetCountAsync<TableModel>();

    }
}
