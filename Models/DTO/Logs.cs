namespace QMRv2.Models.DTO
{
    public class Logs
    {
        public string? ID { get; set; }
        public string? Message { get; set; }
        public string? Reference { get; set; }
        public string? RequestData { get; set; }
        public string? ResponseData { get; set; }
        public string? Verb { get; set; }
        public string? ResponseCode { get; set; }
        public DateTime InsertDate { get; set; }
    }
}
