namespace Hackathon_2025.Models
{
    public class BuyCreditsRequest
    {
        // "plus5" or "plus11"
        public string Pack { get; set; } = "plus5";

        // Optional: allow buying multiple of the same pack in one checkout (defaults to 1)
        public int Quantity { get; set; } = 1;
    }
}