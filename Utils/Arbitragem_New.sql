SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[ExecuteArbitrageCalculation]
AS
BEGIN
    SET NOCOUNT ON;

    -- Create a temporary table to store intermediate results
    CREATE TABLE #TempArbitrageResults (
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
        SiteId_Y INT, 
        League_X NVARCHAR(255),
        League_Y NVARCHAR(255),
        Game_X NVARCHAR(255),
        Game_Y NVARCHAR(255),
        Url_X NVARCHAR(MAX),
        Url_Y NVARCHAR(MAX)
    );

    -- Insert arbitrage calculation results into the temporary table
    INSERT INTO #TempArbitrageResults
    SELECT
        ROUND(
            (((ROUND((1500 * b2.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b1.Multiplier + 
               ROUND((1500 * b1.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b2.Multiplier) / 2) - 1500) / 1500 * 100,
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
        b2.SiteId AS SiteId_Y,
        g1.League AS League_X,
        g2.League AS League_Y,
        g1.GameName AS Game_X,
        g2.GameName AS Game_Y,
        g1.URL AS Url_X,
        g2.URL AS Url_Y
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
        ((b1.OverUnder <> b2.OverUnder AND b1.BetAmount = b2.BetAmount)
        or (b1.OverUnder = 'Mais de' AND b2.OverUnder = 'Menos de' AND b1.BetAmount <= b2.BetAmount)
        or (b1.OverUnder = 'Menos de' AND b2.OverUnder = 'Mais de' AND b1.BetAmount >= b2.BetAmount))
        AND g1.GameDate >= GETDATE()
        AND b1.TagId = b2.TagId
        and (ROUND(
            (((ROUND((1500 * b2.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b1.Multiplier + 
               ROUND((1500 * b1.Multiplier) / (b1.Multiplier + b2.Multiplier), 2) * b2.Multiplier) / 2) - 1500) / 1500 * 100,
            2
        )) > 1;

    -- Update existing records in the ArbitrageResults table
UPDATE ArbitrageResults
SET 
    Arbitrage_Lucro_Percent = t.Arbitrage_Lucro_Percent,
    Multiplier_X = t.Multiplier_X,
    Multiplier_Y = t.Multiplier_Y
FROM ArbitrageResults a
INNER JOIN #TempArbitrageResults t
ON a.SiteId_X = t.SiteId_X 
   AND a.SiteId_Y = t.SiteId_Y 
   AND a.Game_X = t.Game_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.Game_Y = t.Game_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.TagName_X = t.TagName_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.TagName_Y = t.TagName_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.OverUnder_X = t.OverUnder_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.OverUnder_Y = t.OverUnder_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.BetAmount_X = t.BetAmount_X
   AND a.BetAmount_Y = t.BetAmount_Y
   AND a.HomeTeam = t.HomeTeam COLLATE SQL_Latin1_General_CP1_CI_AS
   AND a.AwayTeam = t.AwayTeam COLLATE SQL_Latin1_General_CP1_CI_AS;


    -- Delete records where Arbitrage_Lucro_Percent is less than 1%
DELETE FROM ArbitrageResults
WHERE Arbitrage_Lucro_Percent < 1;

-- Insert new records
INSERT INTO ArbitrageResults
SELECT t.*
FROM #TempArbitrageResults t
LEFT JOIN ArbitrageResults a
ON t.SiteId_X = a.SiteId_X 
   AND t.SiteId_Y = a.SiteId_Y 
   AND t.Game_X = a.Game_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.Game_Y = a.Game_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.TagName_X = a.TagName_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.TagName_Y = a.TagName_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.OverUnder_X = a.OverUnder_X COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.OverUnder_Y = a.OverUnder_Y COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.BetAmount_X = a.BetAmount_X
   AND t.BetAmount_Y = a.BetAmount_Y
   AND t.HomeTeam = a.HomeTeam COLLATE SQL_Latin1_General_CP1_CI_AS
   AND t.AwayTeam = a.AwayTeam COLLATE SQL_Latin1_General_CP1_CI_AS
WHERE a.SiteId_X IS NULL;

-- Drop the temporary table
DROP TABLE #TempArbitrageResults;

-- Select the final results
SELECT * FROM ArbitrageResults
ORDER BY Arbitrage_Lucro_Percent DESC;

END;
GO
