using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HamletSystem : MonoBehaviour
{
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private float shortfallCheckInterval = 2.0f; // log resources and check for shortfalls every 2 seconds
    [SerializeField] private float healthThreshold;

    private List<int> healthHistory = new(); // Normalised health values over time
    private List<float> cdf = new(); // Cumulative probability function for health i.e. P(health < z) at time t

    private void Start()
    {
        // Start checking health at intervals
        InvokeRepeating(nameof(CheckHealthShortfall), 2, shortfallCheckInterval);
    }

    public void UpdateHealthHistory(int normalisedHealth)
    {
        healthHistory.Add(normalisedHealth);
    }

    // Periodically check for potential health shortfall
    private void CheckHealthShortfall()
    {
        // If we have enough health data to compute the CDF
        if (healthHistory.Count > 1)
        {
            CalculateCDF();

            // Now check if the player's health is below the threshold
            float currentHealthNormalized = healthHistory.Last();
            float cdfValue = cdf.Last();

            Debug.Log($"Current Health (Normalized): {currentHealthNormalized}, CDF Value: {cdfValue}");

            if (currentHealthNormalized < healthThreshold)
            {
                Debug.LogWarning("Health shortfall predicted!");
            }
        }
    }

    // Calculate the CDF based on health history
    private void CalculateCDF()
    {
        // Sort the health history
        List<int> sortedHealthHistory = healthHistory.OrderBy(h => h).ToList();

        // Clear previous CDF data
        cdf.Clear();

        // Calculate the CDF
        for (int i = 0; i < sortedHealthHistory.Count; i++)
        {
            // CDF Value: Proportion of values less than or equal to current health
            float cdfValue = (i + 1) / (float)sortedHealthHistory.Count;
            cdf.Add(cdfValue);
        }
    }

    // Optional: Display the CDF for debugging purposes
    private void DisplayCDF()
    {
        for (int i = 0; i < cdf.Count; i++)
        {
            Debug.Log($"Health: {healthHistory[i]}, CDF: {cdf[i]}");
        }
    }
}