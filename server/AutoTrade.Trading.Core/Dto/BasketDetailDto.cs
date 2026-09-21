using System;
using System.Collections.Generic;
using AutoTrade.Trading.Core.Enums;

namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Registry detail: the editable draft plus which published version is currently active.
    /// </summary>
    public class BasketDetailDto
    {
        public Guid BasketId { get; set; }
        public string Name { get; set; }
        public BasketStatus Status { get; set; }
        public int LatestVersionNumber { get; set; }
        public Guid? ActiveVersionId { get; set; }
        public int ActiveVersionNumber { get; set; }
        public List<BasketCompositionLegDto> DraftLegs { get; set; }
        public BasketPolicyDto DraftPolicy { get; set; }

        /// <summary>
        /// Policy frozen in the active version, when this basket is the one holding it. The draft above is what
        /// the operator is preparing: the rules actually in force are these, and they only change on publication.
        /// </summary>
        public BasketPolicyDto ActivePolicy { get; set; }
    }
}
