-- Create the ArbitrageResults table if it doesn't exist
IF OBJECT_ID('ArbitrageResults', 'U') IS NULL
BEGIN
    CREATE TABLE ArbitrageResults (
        Valor_Lucro DECIMAL(18,2),
        Arbitrage_Lucro_Percent DECIMAL(18,2),
        TagName_X NVARCHAR(255),
        OverUnder_X NVARCHAR(50),
        BetAmount_X DECIMAL(18,2),
        Multiplier_X DECIMAL(18,2),
        HomeTeam NVARCHAR(255),
        SiteName_X NVARCHAR(255),
        SiteName_Y NVARCHAR(255),
        AwayTeam NVARCHAR(255),
        OverUnder_Y NVARCHAR(50),
        BetAmount_Y DECIMAL(18,2),
        Multiplier_Y DECIMAL(18,2),
        TagName_Y NVARCHAR(255),
        SiteId_X INT,
        GameDate_X DATETIME,
        BetId_X INT,
        BetId_Y INT,
        SiteId_Y INT,
        Stake_X DECIMAL(18,2),
        Stake_Y DECIMAL(18,2)
    );
END
ELSE
BEGIN
    -- Clear existing data from ArbitrageResults
    TRUNCATE TABLE ArbitrageResults;
END

-- Insert arbitrage calculation results into the ArbitrageResults table
INSERT INTO ArbitrageResults
SELECT 
    ROUND(
        ((ROUND((500 * b2.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b1.Multiplier + 
          ROUND((500 * b1.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b2.Multiplier) / 2) - 500, 
        2
    ) AS Valor_Lucro,
    ROUND(
        (((ROUND((500 * b2.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b1.Multiplier + 
           ROUND((500 * b1.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b2.Multiplier) / 2) - 500) / 500 * 100,
        2
    ) AS Arbitrage_Lucro_Percent,
    b1.TagName AS TagName_X,    
    b1.OverUnder AS OverUnder_X, 
    b1.BetAmount AS BetAmount_X, 
    b1.Multiplier AS Multiplier_X,       
    t1.Aliases AS HomeTeam,
    s1.Name AS SiteName_X,
    s2.Name AS SiteName_Y,
    t2.Aliases AS AwayTeam,              
    b2.OverUnder AS OverUnder_Y, 
    b2.BetAmount AS BetAmount_Y,
    b2.Multiplier AS Multiplier_Y,
    b2.TagName AS TagName_Y, 
    b1.SiteId AS SiteId_X,          
    g1.GameDate AS GameDate_X,     
    b1.BetId AS BetId_X,  
    b2.BetId AS BetId_Y,  
    b2.SiteId AS SiteId_Y,
    ROUND((500 * b2.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) AS Stake_X,
    ROUND((500 * b1.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) AS Stake_Y
FROM 
    (SELECT DISTINCT Name, SiteId FROM Site) s1
JOIN 
    (SELECT DISTINCT Name, SiteId FROM Site) s2
    ON s1.SiteId < s2.SiteId -- Ensure unique combinations without inversions
JOIN 
    BetInfo b1 ON b1.SiteId = s1.SiteId
JOIN 
    GamesInfo g1 ON b1.GameId = g1.GameId
JOIN 
    BetInfo b2 ON b2.SiteId = s2.SiteId AND b1.GameId <> b2.GameId
JOIN 
    GamesInfo g2 ON b2.GameId = g2.GameId
    AND g1.HomeTeamId = g2.HomeTeamId
    AND g1.AwayTeamId = g2.AwayTeamId
    AND g1.GameDate = g2.GameDate
JOIN
    Teams t1 ON g1.HomeTeamId = t1.TeamId
JOIN 
    Teams t2 ON g1.AwayTeamId = t2.TeamId
WHERE 
    (b1.OverUnder <> b2.OverUnder)
    AND g1.GameDate >= GETDATE()
    AND b1.TagId = b2.TagId
    AND b1.BetAmount = b2.BetAmount;

-- Select the final results
SELECT * FROM ArbitrageResults
ORDER BY Valor_Lucro DESC;
