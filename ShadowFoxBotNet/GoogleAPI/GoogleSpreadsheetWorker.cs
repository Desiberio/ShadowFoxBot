using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowFoxBotNet
{
    class GoogleSpreadsheetWorker
    {
        static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
        static readonly string ApplicationName = "SFVBot";
        static readonly string spreadsheetID = "1xLac1YTjcIC06d-Jqk2ryadcgLVZ1TilE6ADkuhcWug";
        static string spreadsheetName;
        static SheetsService service;
        public GoogleSpreadsheetWorker()
        {
            GoogleCredential credential;
            using (var stream = new FileStream("./credentials.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }
            service = new SheetsService(new BaseClientService.Initializer() { HttpClientInitializer = credential, ApplicationName = ApplicationName });
            var ssRequest = service.Spreadsheets.Get(spreadsheetID);
            Spreadsheet ss = ssRequest.Execute();
            spreadsheetName = ss.Sheets[2].Properties.Title;
        }

        public bool CheckIfCellIsNull(string cell)
        {
            var request = service.Spreadsheets.Values.Get(spreadsheetID, cell);

            var response = request.Execute();
            var values = response.Values;

            if(values != null && values.Count > 0)
            {
                return false;
            }
            return true;
        }
       
        public List<int> GetCellsNumberInRows(string range, int numberOfRows)
        {
            List<int> result = new List<int>(numberOfRows);
            var request = service.Spreadsheets.Values.Get(spreadsheetID, range);

            var response = request.Execute();
            var values = response.Values;

            if (values != null && values.Count > 0)
            {
                foreach(var row in values)
                {
                    result.Add(row.Count);
                }
            }

            return result;
        }

        public void UpdateEntries(string range, List<object> objectList)
        {
            ValueRange valueRange = new ValueRange();
            valueRange.Values = new List<IList<object>> { objectList };
            valueRange.MajorDimension = "COLUMNS";

            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetID, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            var updateResponse = updateRequest.Execute();
        }

        public void AddEntries(string range, List<object> objectList)
        {
            ValueRange valueRange = new ValueRange();
            valueRange.Values = new List<IList<object>> { objectList };
            valueRange.MajorDimension = "ROWS";
            valueRange.Range = range;

            var updateRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetID, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            var updateResponse = updateRequest.Execute();
        }

        public bool AddChampionsToSpreadsheet(string summonerName, string ownerName, Region region = Region.RU)
        {
            try
            {
                RiotGamesAPI riotGamesAPI = new RiotGamesAPI();
                string summonerID = riotGamesAPI.GetSummonerIdByName(summonerName, region);
                List<Champion> _7masteryChamps = riotGamesAPI.GetChampions7Mastery(summonerID, region);

                List<string> championsSpreadsheet = GetColumn($"{spreadsheetName}!B3:B200");
                List<int> numberOfOccupiedCellsInRow = GetCellsNumberInRows($"{spreadsheetName}!D3:Z256", championsSpreadsheet.Count);

                string fullName = summonerName;
                if (summonerName == ownerName) fullName = summonerName;
                else fullName = $"{summonerName} ({ownerName})";

                foreach (Champion champion in _7masteryChamps)
                {
                    if (champion.WasAddedToGoogleDocs) continue;

                    int index = championsSpreadsheet.FindIndex(x => x == champion.Name);
                    char column = (char)('D' + numberOfOccupiedCellsInRow[index]);
                    if (column > 'Z') throw new Exception("There are no more columns in table (looks like this method needs an upgrade).");
                    string cell = $"{spreadsheetName}!{column}{3 + index}";
                    AddEntries(cell, new List<object>() { fullName });
                    champion.WasAddedToGoogleDocs = true;
                }

                riotGamesAPI.SaveChampionsData(_7masteryChamps);
                return true;
            }
            catch (RiotGamesAPIException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public List<string> GetColumn(string range)
        {
            List<string> columnData = new List<string>();
            var request = service.Spreadsheets.Values.Get(spreadsheetID, range);
            var response = request.Execute();
            IList<IList<object>> values = response.Values;
            if(values != null && values.Count > 0) foreach (var row in values) columnData.Add(row[0].ToString());
            return columnData;
        }
    }
}
