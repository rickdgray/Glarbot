using Google.Apis.Sheets.v4.Data;

namespace Glarbot
{
    internal interface IGoogleSheetsService
    {
        Task<ValueRange> GetAsync(string range, CancellationToken cancellationToken);
        Task UpdateAsync(string range, string value, CancellationToken cancellationToken);
        Task AppendAsync(string range, IList<string> values, CancellationToken cancellationToken);
    }
}