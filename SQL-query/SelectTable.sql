use agentsystemdb
select count(*)
	from ListStationData
	where Time >= '2020-04-14 00:08:33.000' and Time <= '2020-04-14 00:08:33.999'
	