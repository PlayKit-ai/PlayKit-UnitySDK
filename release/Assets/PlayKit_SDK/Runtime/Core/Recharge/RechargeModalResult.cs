namespace PlayKit_SDK.Recharge
{
    /// <summary>
    /// Result of the recharge modal interaction.
    /// Contains information about user's choice and any errors.
    /// </summary>
    public class RechargeModalResult
    {
        /// <summary>Whether the user confirmed the action</summary>
        public bool Confirmed { get; set; }

        /// <summary>Selected product SKU (may be null for browser recharge)</summary>
        public string SelectedSku { get; set; }

        /// <summary>Error message if something went wrong</summary>
        public string Error { get; set; }

        /// <summary>
        /// Create a cancelled result
        /// </summary>
        public static RechargeModalResult Cancelled(string error = null)
        {
            return new RechargeModalResult
            {
                Confirmed = false,
                Error = error
            };
        }

        /// <summary>
        /// Create a confirmed result
        /// </summary>
        public static RechargeModalResult Success(string selectedSku = null)
        {
            return new RechargeModalResult
            {
                Confirmed = true,
                SelectedSku = selectedSku
            };
        }

        /// <summary>
        /// Create an error result
        /// </summary>
        public static RechargeModalResult Failed(string error)
        {
            return new RechargeModalResult
            {
                Confirmed = false,
                Error = error
            };
        }
    }
}
