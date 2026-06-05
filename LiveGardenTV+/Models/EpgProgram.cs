using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using LiveGardenTVPlus.Models;

namespace LiveGardenTVPlus.Services
{
    public class EpgService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<EpgChannel> _channels = new List<EpgChannel>();

        public async Task LoadEpgAsync(string epgUrl)
        {
            if (string.IsNullOrEmpty(epgUrl))
                return;

            Debug.WriteLine($"EPG loading started for {epgUrl}");

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var response = await client.GetAsync(epgUrl);
                response.EnsureSuccessStatusCode();

                Debug.WriteLine($"EPG downloaded, content length: {response.Content.Headers.ContentLength}");

                string tempFile = Path.Combine(Path.GetTempPath(), "epg_test.xml");
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(tempFile))
                {
                    if (epgUrl.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var gzipStream = new GZipStream(contentStream, CompressionMode.Decompress))
                            await gzipStream.CopyToAsync(fileStream);
                    }
                    else
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
                Debug.WriteLine($"EPG saved to {tempFile}");

                // Parse the saved file
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (var fileStream = File.OpenRead(tempFile))
                using (var reader = XmlReader.Create(fileStream, settings))
                {
                    EpgChannel currentChannel = null;
                    EpgProgramme currentProgram = null;

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "channel":
                                    string id = reader.GetAttribute("id");
                                    currentChannel = new EpgChannel { Id = id };
                                    _channels.Add(currentChannel);
                                    break;
                                case "display-name":
                                    if (currentChannel != null && reader.Read())
                                        currentChannel.DisplayName = reader.Value.Trim();
                                    break;
                                case "icon":
                                    if (currentChannel != null)
                                        currentChannel.Icon = reader.GetAttribute("src");
                                    break;
                                case "programme":
                                    string startStr = reader.GetAttribute("start");
                                    string stopStr = reader.GetAttribute("stop");
                                    string channelId = reader.GetAttribute("channel");
                                    currentProgram = new EpgProgramme
                                    {
                                        Start = ParseEpgDate(startStr),
                                        Stop = ParseEpgDate(stopStr)
                                    };
                                    var channel = _channels.FirstOrDefault(c => c.Id == channelId);
                                    if (channel != null)
                                        channel.Programmes.Add(currentProgram);
                                    break;
                                case "title":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Title = reader.Value.Trim();
                                    break;
                                case "desc":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Description = reader.Value.Trim();
                                    break;
                                case "category":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Category = reader.Value.Trim();
                                    break;
                            }
                        }
                    }
                }

                Debug.WriteLine("EPG parsing completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EPG load error: {ex.Message}");
            }
        }

        private DateTime ParseEpgDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;

            // Example: "20260303205100 +0200" or "20260303205100"
            var parts = dateStr.Trim().Split(' ');
            string datePart = parts[0];
            if (datePart.Length < 14)
                return DateTime.MinValue;

            int year = int.Parse(datePart.Substring(0, 4));
            int month = int.Parse(datePart.Substring(4, 2));
            int day = int.Parse(datePart.Substring(6, 2));
            int hour = int.Parse(datePart.Substring(8, 2));
            int minute = int.Parse(datePart.Substring(10, 2));
            int second = int.Parse(datePart.Substring(12, 2));

            DateTime local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

            if (parts.Length > 1 && parts[1].Length >= 5)
            {
                // Offset format: +0200 or -0500
                string offsetStr = parts[1];
                int sign = offsetStr[0] == '+' ? 1 : -1;
                int offHours = int.Parse(offsetStr.Substring(1, 2));
                int offMins = int.Parse(offsetStr.Substring(3, 2));
                TimeSpan offset = new TimeSpan(sign * offHours, sign * offMins, 0);
                DateTimeOffset dto = new DateTimeOffset(local, offset);
                return dto.UtcDateTime;
            }
            else
            {
                // No offset: assume UTC
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            }
        }

        public EpgProgramme GetCurrentProgram(string tvgId, DateTime nowUtc)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == tvgId);
            if (channel == null) return null;
            return channel.Programmes.FirstOrDefault(p => p.Start <= nowUtc && p.Stop > nowUtc);
        }
    }
}