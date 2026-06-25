using System;
using System.Collections.Generic;
using System.Linq;
using LiveGardenTVPlus.Models;
using Newtonsoft.Json.Linq;

namespace LiveGardenTVPlus.Services
{
    public static class JsonMapper
    {
        public static List<ChannelJson> MapFromJson(string jsonText, List<MappingConfig> mappings)
        {
            var result = new List<ChannelJson>();
            try
            {
                var token = JToken.Parse(jsonText);
                JArray array = null;
                if (token is JArray arr)
                    array = arr;
                else if (token is JObject obj)
                {
                    foreach (var prop in obj.Properties())
                    {
                        if (prop.Value is JArray)
                        {
                            array = (JArray)prop.Value;
                            break;
                        }
                    }
                }
                if (array == null)
                    return result;

                foreach (var child in array.Children())
                {
                    if (child is not JObject item)
                        continue;

                    var channel = new ChannelJson();
                    bool hasValidData = false;
                    try
                    {
                        foreach (var mapping in mappings)
                        {
                            if (string.IsNullOrEmpty(mapping.SourcePropertyName)) continue;
                            JToken value = item.SelectToken(mapping.SourcePropertyName);
                            if (value == null || value.Type == JTokenType.Null) continue;

                            switch (mapping.TargetField)
                            {
                                case "name":
                                    channel.name = value.ToString();
                                    if (!string.IsNullOrEmpty(channel.name)) hasValidData = true;
                                    break;
                                case "stream_urls":
                                    if (value.Type == JTokenType.Array)
                                        channel.stream_urls = value.Select(v => v.ToString()).Where(u => !string.IsNullOrEmpty(u)).ToList();
                                    else if (value.Type == JTokenType.String)
                                    {
                                        string url = value.ToString();
                                        if (!string.IsNullOrEmpty(url))
                                            channel.stream_urls = new List<string> { url };
                                    }
                                    if (channel.stream_urls != null && channel.stream_urls.Any(u => !string.IsNullOrEmpty(u))) hasValidData = true;
                                    break;
                                case "logo_url":
                                    channel.logo_url = value.ToString();
                                    break;
                                case "group":
                                    channel.group = value.ToString();
                                    break;
                                case "tvg_id":
                                    channel.tvg_id = value.ToString();
                                    break;
                                case "isFavorite":
                                    if (value.Type == JTokenType.Boolean)
                                        channel.isFavorite = value.Value<bool>();
                                    else
                                        channel.isFavorite = value.ToString().ToLower() == "true";
                                    break;
                                case "country":
                                    channel.country = value.ToString();
                                    break;
                                case "languages":
                                    if (value.Type == JTokenType.Array)
                                        channel.languages = value.Select(v => v.ToString()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                                    else if (value.Type == JTokenType.String)
                                        channel.languages = value.ToString().Split(',').Select(s => s.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                                    break;
                                case "youtube_urls":
                                    if (value.Type == JTokenType.Array)
                                        channel.youtube_urls = value.Select(v => v.ToString()).Where(u => !string.IsNullOrEmpty(u)).ToList();
                                    else if (value.Type == JTokenType.String)
                                    {
                                        string url = value.ToString();
                                        if (!string.IsNullOrEmpty(url))
                                            channel.youtube_urls = new List<string> { url };
                                    }
                                    break;
                                case "nanoid":
                                    channel.nanoid = value.ToString();
                                    break;
                                case "isGeoBlocked":
                                    if (value.Type == JTokenType.Boolean)
                                        channel.isGeoBlocked = value.Value<bool>();
                                    else
                                        channel.isGeoBlocked = value.ToString().ToLower() == "true";
                                    break;
                            }
                        }
                        if (hasValidData)
                            result.Add(channel);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error mapping item: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Item: {item.ToString()}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MapFromJson error: {ex.Message}");
            }
            return result;
        }
    }
}