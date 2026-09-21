namespace AutoTrade.Trading.Core.Dto
{
    /// <summary>
    /// Embedding as the application sees it. It is reported next to the store because they are independent
    /// facts: a store that is open and an embedding source that is missing mean the semantic memory cannot
    /// answer a retrieval, and an operator must be able to see which of the two is missing.
    /// </summary>
    public class EmbeddingSourceStatusDto
    {
        /// <summary>Engine in force, for example None or JigenOnnx.</summary>
        public string Engine { get; set; }

        /// <summary>Model producing the vectors. Reported even when unavailable, so what was asked for is visible.</summary>
        public string Model { get; set; }

        /// <summary>Identifies the rendering and the model together, as stored in a collection name.</summary>
        public string TextVersion { get; set; }

        public bool IsAvailable { get; set; }
    }
}
