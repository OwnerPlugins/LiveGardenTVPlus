using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using LiveGardenTVPlus.Models;

namespace LiveGardenTVPlus.Services
{
    public static class JsonMapper
    {
        /// <summary>
        /// Maps a JSON string to a list of ChannelJson objects using the provided mappings.
        /// </summary>
        public static List<ChannelJson> MapFromJson(string jsonText, List<MappingConfig> mappings)
        {
            var array = JArray.Parse(jsonText);
            var result = new List<ChannelJson>();

            foreach (var item in array)
            {
                var ch = new ChannelJson();
                foreach (var map in mappings)
                {
                    JToken token = item.SelectToken(map.SourcePropertyName);
                    if (token == null) continue;

                    switch (map.TargetField)
                    {
                        case "name":
                            ch.name = token.ToString();
                            break;
                        case "stream_urls":
                            ch.stream_urls = ParseStringList(token);
                            break;
                        case "logo_url":
                            ch.logo_url = token.ToString();
                            break;
                        case "group":
                            ch.group = token.ToString();
                            break;
                        case "tvg_id":
                            ch.tvg_id = token.ToString();
                            break;
                        case "isFavorite":
                            ch.isFavorite = ParseBool(token);
                            break;
                        case "country":
                            ch.country = token.ToString();
                            break;
                        case "languages":
                            ch.languages = ParseStringList(token);
                            break;
                        case "youtube_urls":
                            ch.youtube_urls = ParseStringList(token);
                            break;
                        case "nanoid":
                            ch.nanoid = token.ToString();
                            break;
                        case "isGeoBlocked":
                            ch.isGeoBlocked = ParseBool(token);
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(ch.name))
                    result.Add(ch);
            }
            return result;
        }

        private static List<string> ParseStringList(JToken token)
        {
            if (token.Type == JTokenType.Array)
                return token.Select(t => t.ToString()).ToList();
            if (token.Type == JTokenType.String)
            {
                string str = token.ToString();
                if (str.Contains(','))
                    return str.Split(',').Select(s => s.Trim()).ToList();
                return new List<string> { str };
            }
            return new List<string>();
        }

        private static bool ParseBool(JToken token)
        {
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            if (token.Type == JTokenType.String)
                return token.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       token.ToString() == "1";
            if (token.Type == JTokenType.Integer)
                return token.Value<int>() != 0;
            return false;
        }
    }
}