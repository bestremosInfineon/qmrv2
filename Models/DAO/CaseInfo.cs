namespace QMRv2.Models.DAO
{
    public class CaseInfo
    {
        public string? CaseNumber { get; set; }
        public string? Subject { get; set; }
        public string? ProblemDescription { get; set; }
        public string? Originator { get; set; }
        public string? Owner { get; set; }
        public string? DateCreated { get; set; }
        public string? DateModified { get; set; }
        public string? Status { get; set; }
        public string? QMR { get; set; }

        public string? LotNumber { get; set; }
        public string? SplitNumber { get; set; }
        public string? MfgSiteName { get; set; }
        public string? MfgSite { get; set; }
        public string? MfgArea { get; set; }
        public string? Qty { get; set; }
        public string? Step { get; set; }
        public string? WarehouseLoc { get; set; }
        public string? Device { get; set; }
        public string? Pkg { get; set; }
        public string? ShipLoc { get; set; }
        public string? ProductLine { get; set; }
        public string? CMS_MRBNo { get; set; }
        public string? Parent_Lot { get; set; }
        public string? HoldType { get; set; }
        public string? HoldSystem { get; set; }
        public string? NcCategory { get; set; }
        public string? MrbType { get; set; }
        public string? DetectingSite { get; set; }
        public string? CauseOwner { get; set; }
        public int IsFound { get; set; }
        public string? DeviationCaseId { get; set; }
        public string? DataSource { get; set; }
        public string? LotTraceOrigin { get; set; }
        public int ID { get; set; }
        public string? Prod_Facility { get; set; }
        public string? IFX_LOT_NO { get; set; }
        public string? ORIGINAL_LOT_NO { get; set; }
        public string? ECNOwner { get; set; }
        public string? TransferID { get; set; }
        public string? TraceOrder { get; set; }
        public string? IfxLotName { get; set; }
    }
}
