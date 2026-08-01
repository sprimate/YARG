using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace YARG
{
    /// <summary>
    /// One row of the cloud tour catalogue, as returned by GET /api/tours.
    /// </summary>
    public class CloudTourMetadata
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("author")] public string Author;
        [JsonProperty("downloads")] public int Downloads;
        [JsonProperty("rating_sum")] public int RatingSum;
        [JsonProperty("rating_count")] public int RatingCount;
        [JsonProperty("updated_at")] public string UpdatedAt;

        [JsonIgnore]
        public float AverageRating => RatingCount > 0 ? (float) RatingSum / RatingCount : 0f;
    }

    /// <summary>
    /// Thin HTTP client for the yarg-tours Cloudflare worker. Every method
    /// throws on failure — callers are expected to surface errors to the user.
    /// </summary>
    public static class TourCloudService
    {
        private const string BASE_URL = "https://yarg-tours.sprimatestudios.workers.dev";//(sprimate TODO) - Add this as a setting

        private const string RATING_PREF_KEY_PREFIX = "CloudTourRating_";

        /// <summary>
        /// The rating (1-5) this install has given a tour, or 0 if it hasn't
        /// rated it. Ratings are tracked locally; the server only stores the
        /// aggregates.
        /// </summary>
        public static int GetLocalRating(string tourId)
        {
            return PlayerPrefs.GetInt(RATING_PREF_KEY_PREFIX + tourId.ToLowerInvariant(), 0);
        }

        private static void SetLocalRating(string tourId, int stars)
        {
            PlayerPrefs.SetInt(RATING_PREF_KEY_PREFIX + tourId.ToLowerInvariant(), stars);
            PlayerPrefs.Save();
        }

        private class TourListResponse
        {
            [JsonProperty("tours")] public List<CloudTourMetadata> Tours;
        }

        private class DownloadResponse
        {
            [JsonProperty("downloads")] public int Downloads;
        }

        private class RateResponse
        {
            [JsonProperty("ratingSum")] public int RatingSum;
            [JsonProperty("ratingCount")] public int RatingCount;
        }

        /// <summary>
        /// Fetches the entire tour catalogue (metadata only).
        /// </summary>
        public static async UniTask<List<CloudTourMetadata>> GetTourList()
        {
            string body = await Request(UnityWebRequest.kHttpVerbGET, "/api/tours");

            var response = JsonConvert.DeserializeObject<TourListResponse>(body);
            if (response?.Tours == null)
            {
                throw new InvalidOperationException(
                    $"Cloud tour list response was malformed: {body}");
            }

            return response.Tours;
        }

        /// <summary>
        /// Downloads the full tour JSON for one tour. Does not count as a
        /// download — call <see cref="RecordDownload"/> after saving to disk.
        /// </summary>
        public static async UniTask<string> DownloadTourJson(string tourId)
        {
            return await Request(UnityWebRequest.kHttpVerbGET, $"/api/tours/{tourId}/data");
        }

        /// <summary>
        /// Increments the download counter. Only call this after the tour has
        /// actually been saved to disk. Returns the new download count.
        /// </summary>
        public static async UniTask<int> RecordDownload(string tourId)
        {
            string body = await Request(UnityWebRequest.kHttpVerbPOST,
                $"/api/tours/{tourId}/download");
            return JsonConvert.DeserializeObject<DownloadResponse>(body).Downloads;
        }

        /// <summary>
        /// Uploads (or updates) a tour. The worker extracts the metadata from
        /// the JSON itself.
        /// </summary>
        public static async UniTask UploadTour(TourData tour)
        {
            string json = JsonConvert.SerializeObject(tour, Formatting.None);
            await Request(UnityWebRequest.kHttpVerbPOST, "/api/tours", json);
        }

        /// <summary>
        /// Rates a tour 1-5 stars, replacing any previous rating this install
        /// made (the server adjusts its aggregates as if the old rating were
        /// removed and the new one added). Returns the new aggregate (sum, count).
        /// </summary>
        public static async UniTask<(int RatingSum, int RatingCount)> RateTour(string tourId, int stars)
        {
            if (stars < 1 || stars > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(stars), "Rating must be 1-5 stars.");
            }

            int previous = GetLocalRating(tourId);
            string payload = previous > 0
                ? JsonConvert.SerializeObject(new { stars, previousStars = previous })
                : JsonConvert.SerializeObject(new { stars });

            string body = await Request(UnityWebRequest.kHttpVerbPOST,
                $"/api/tours/{tourId}/rate", payload);

            // Only remember the rating once the server accepted it.
            SetLocalRating(tourId, stars);

            var response = JsonConvert.DeserializeObject<RateResponse>(body);
            return (response.RatingSum, response.RatingCount);
        }

        private static async UniTask<string> Request(string method, string path, string jsonBody = null)
        {
            using var request = new UnityWebRequest(BASE_URL + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            await request.SendWebRequest().ToUniTask();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Cloud request {method} {path} failed: {request.error} — {request.downloadHandler.text}");
            }

            return request.downloadHandler.text;
        }
    }
}
