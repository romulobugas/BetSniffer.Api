declare @MaxOdd DECIMAL(2,1), @MinOdd DECIMAL(2,1);

set @MaxOdd = 2;
set @MinOdd = 1.5

select distinct
    * 
from ArbitrageResults 
where (Multiplier_y > @MaxOdd and Multiplier_X > @MinOdd) or (Multiplier_y > @MinOdd and Multiplier_X > @MaxOdd)
and GameDate_X > GETDATE()
order by 6 desc
