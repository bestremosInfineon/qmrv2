using Newtonsoft.Json;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;

namespace QMRv2.Common
{
    public class Converters
    {
        public string GetFormattedJson(object value)
        {
            return JsonConvert.SerializeObject(value, new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-dd HH:mm",
                Converters = new List<JsonConverter>() { new Newtonsoft.Json.Converters.StringEnumConverter() }
            });
        }

        // Validation method to check if QmrNumber, TransferID, and LotNumber are null or whitespace
        public bool ValidateProperties(LotRequest query, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (query.LotList != null)
            {
                if (string.IsNullOrWhiteSpace(query.QmrNumber))
                {
                    errorMessage = "QmrNumber cannot be null or whitespace.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(query.TransferID))
                {
                    errorMessage = "TransferID cannot be null or whitespace.";
                    return false;
                }

                foreach (var lot in query.LotList)
                {
                    if (string.IsNullOrWhiteSpace(lot.LotNumber))
                    {
                        errorMessage = "LotNumber cannot be null or whitespace.";
                        return false;
                    }

                    if (lot.LotNumber.Length > 30)
                    {
                        errorMessage = "LotNumber exceeded character limit.";
                        return false;
                    }
                }
            }
            else
            {
                errorMessage = "LotList cannot be null.";
                return false;
            }

            return true;
        }


        public IfxBlockResult LotSplice(string rowlot)
        {
            var response = new IfxBlockResult();
            if ("123456789".Contains(rowlot[0].ToString()))
            {
                if (rowlot.Length >= 9 && (rowlot.StartsWith("6") || rowlot.StartsWith("7") || rowlot.StartsWith("12")))
                {
                    response.LotNumber = rowlot.Substring(0, 9);
                    response.Split = rowlot.Substring(9);
                }
                else if (rowlot.Length >= 7 && ("12345789".Contains(rowlot[0].ToString())))
                {
                    response.LotNumber = rowlot.Substring(0, 7);
                    response.Split = rowlot.Substring(7);
                }
            }

            return response;
        }
    }
}
