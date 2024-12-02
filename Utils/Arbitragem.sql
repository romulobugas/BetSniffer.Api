WITH ArbitrageCalculation AS (
    SELECT 
        b1.BetId AS BetId_X,
        b1.SiteId AS SiteId_X,
        s1.[Name] AS SiteName_X,
        b1.TagName AS TagName_X,
        b1.OverUnder AS OverUnder_X,
        b1.BetAmount AS BetAmount_X,
        b1.Multiplier AS Multiplier_X,
        b2.BetId AS BetId_Y,
        b2.SiteId AS SiteId_Y,
        s2.Name AS SiteName_Y,
        b2.TagName AS TagName_Y,
        b2.OverUnder AS OverUnder_Y,
        b2.BetAmount AS BetAmount_Y,
        b2.Multiplier AS Multiplier_Y
    FROM 
        BetInfo b1
    JOIN 
        BetInfo b2 ON b1.GameId <> b2.GameId AND b1.BetAmount = b2.BetAmount
    JOIN
        [Site] s1 ON b1.SiteId = s1.SiteId
    JOIN
        [Site] s2 ON b2.SiteId = s2.SiteId
    WHERE 
        ((b1.OverUnder = 'Mais de' AND b2.OverUnder = 'Menos de') 
        OR (b1.OverUnder = 'Menos de' AND b2.OverUnder = 'Mais de'))
        AND b1.SiteId = 1  -- SiteId do Site X
        AND b2.SiteId = 2  -- SiteId do Site Y
        AND (
            (b1.TagName = 'Escanteios. Total' AND b2.TagName = 'Total de Escanteios ??') 
            OR 
            (b1.TagName = 'Total de Escanteios ??' AND b2.TagName = 'Escanteios. Total')
        )
),
DefinedStake AS (
    -- Definindo o valor total apostado (500) aqui
    SELECT 500 AS Total_Aposta
)
SELECT 
    BetId_X, 
    SiteId_X,
    SiteName_X, 
    TagName_X, 
    OverUnder_X, 
    BetAmount_X, 
    Multiplier_X, 
    BetId_Y, 
    SiteId_Y,
    SiteName_Y, 
    TagName_Y, 
    OverUnder_Y, 
    BetAmount_Y, 
    Multiplier_Y,
    
    -- Calculando as stakes corretamente
    ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) AS Stake_X,  -- Aposta para o mercado X
    ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) AS Stake_Y,  -- Aposta para o mercado Y
    
    -- Calculando o valor final apostado e a perda/lucro
    ROUND(
        ((ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_X + ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_Y) / 2) - Total_Aposta, 
        2
    ) AS Valor_Perda,

    -- Corrigindo o cálculo do percentual de Arbitrage_Lucro_Percent com o sinal correto
    ROUND(
        (((ROUND((Total_Aposta * Multiplier_Y) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_X + ROUND((Total_Aposta * Multiplier_X) / (Multiplier_X + Multiplier_Y), 2) * Multiplier_Y) / 2) - Total_Aposta) / Total_Aposta * 100,
        2
    ) AS Arbitrage_Lucro_Percent
FROM ArbitrageCalculation, DefinedStake;