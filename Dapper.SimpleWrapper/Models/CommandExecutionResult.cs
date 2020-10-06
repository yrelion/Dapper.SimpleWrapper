namespace Dapper.SimpleWrapper.Models
{
    public class CommandExecutionResult
    {
        public DynamicParameters Parameters { get; set; }
        public int RowsAffected { get; set; }
    }
}
