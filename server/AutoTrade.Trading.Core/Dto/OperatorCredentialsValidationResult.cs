using System;

namespace AutoTrade.Trading.Core.Dto
{
    public class OperatorCredentialsValidationResult
    {
        public bool IsFound { get; set; }
        public bool IsValid { get; set; }
        public Guid OperatorId { get; set; }
        public bool IsLockedOut { get; set; }
        public bool IsActive { get; set; }
    }
}
