namespace ShadowFoxBotNet
{
    public enum Region
    {
        RU,
        EUW,
        EUNE,
        NA
    }

    enum GuildInfo : ulong
    {
        //newsChannelID = 887232193376702474, //debug
        newsChannelID = 436522034231640064,
        generalLoLChannelID = 887232193376702474,
        //guildID = 421901237257109523, //debug
        guildID = 338355570669256705
    }

    public enum ErrorCode
    {
        NotFound,
        RoutingValueNotFound,
        BadRequest,
        AlreadyOwned
    }

    enum RoutingType
    {
        Summoner,
        ChampionMastery,
        ChampionMasteryScores
    }
}
