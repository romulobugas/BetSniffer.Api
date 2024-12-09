WITH ArbitrageCalculation AS (
    SELECT 
        b1.BetId AS BetId_X,
        b1.SiteId AS SiteId_X,
        s1.[Name] AS SiteName_X,
        b1.TagName AS TagName_X,
        b1.OverUnder AS OverUnder_X,
        b1.BetAmount AS BetAmount_X,
        b1.Multiplier AS Multiplier_X,
        g1.GameDate AS GameDate_X,
        g1.HomeTeamId AS HomeTeamId_X,
        g1.AwayTeamId AS AwayTeamId_X,
        b2.BetId AS BetId_Y,
        b2.SiteId AS SiteId_Y,
        s2.Name AS SiteName_Y,
        b2.TagName AS TagName_Y,
        b2.OverUnder AS OverUnder_Y,
        b2.BetAmount AS BetAmount_Y,
        b2.Multiplier AS Multiplier_Y,
        g2.GameDate AS GameDate_Y,
        g2.HomeTeamId AS HomeTeamId_Y,
        g2.AwayTeamId AS AwayTeamId_Y
    FROM 
        BetInfo b1
    JOIN 
        GamesInfo g1 ON b1.GameId = g1.GameId
    JOIN 
        BetInfo b2 ON b1.GameId <> b2.GameId 
    JOIN 
        GamesInfo g2 ON b2.GameId = g2.GameId
        AND g1.HomeTeamId = g2.HomeTeamId
        AND g1.AwayTeamId = g2.AwayTeamId
        AND g1.GameDate = g2.GameDate
    JOIN
        [Site] s1 ON b1.SiteId = s1.SiteId
    JOIN
        [Site] s2 ON b2.SiteId = s2.SiteId
    WHERE 
        (b1.OverUnder <> b2.OverUnder)
        AND b1.SiteId = 1  -- Novibet
        AND b2.SiteId = 2  -- Parimach
        and g1.GameDate >= GETDATE() and g2.GameDate >= GETDATE()
        AND (
            b1.TagId = b2.TagId
        )
        AND b1.BetAmount = b2.BetAmount -- Mesma linha de aposta (mesmo total de escanteios ou Cartões)
),
DefinedStake AS (
    SELECT 500 AS Total_Aposta
)
SELECT 

-- Calculando lucro/perda
    ROUND(
        ((ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_X + 
          ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_Y) / 2) - Total_Aposta, 
        2
    ) AS Valor_Lucro,

    -- Calculando percentual de lucro
    ROUND(
        (((ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_X + 
           ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_Y) / 2) - Total_Aposta) / Total_Aposta * 100,
        2
    ) AS Arbitrage_Lucro_Percent,
    TagName_X,    
    OverUnder_X, 
    BetAmount_X, 
    Multiplier_X,       
    t1.Aliases AS HomeTeam,
    SiteName_X,
    SiteName_Y,
    t2.Aliases AS AwayTeam,              
    OverUnder_Y, 
    BetAmount_Y,
    Multiplier_Y,
    TagName_Y, 
    SiteId_X,          
    GameDate_X,     
    
    BetId_X,  
    BetId_Y,  
    SiteId_Y,
    -- Calculando as stakes
    ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) AS Stake_X,
    ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) AS Stake_Y
    
    
FROM 
    ArbitrageCalculation ac
JOIN 
    Teams t1 ON ac.HomeTeamId_X = t1.TeamId
JOIN 
    Teams t2 ON ac.AwayTeamId_X = t2.TeamId,
    DefinedStake

	
order by 1 desc;