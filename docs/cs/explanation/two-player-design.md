# Vysvětlení: jak funguje hra ve dvou

Dvě zařízení ukazují stejný zápas. Tahle stránka vysvětluje, jak se shodnou a
proč je to postavené právě takhle, a ne jinak.

## Rozhoduje jedno zařízení

Zařízení, které hru založilo – hostitel – řídí celý svět přesně jako v sólové
hře: boty, prdy, příšeru, skóre i to, kdo vyhrál. Dvacetkrát za sekundu pošle
druhému zařízení úplný popis toho světa. Připojené zařízení nic z toho
neřídí. Ukazuje, co se mu řekne.

Alternativa – obě zařízení simulují a vyměňují si události – reaguje rychleji,
ale zařízení se můžou rozejít: dva hráči se ve stejném okamžiku dotknou
stejného prdu a každé zařízení ho dá svému hráči. Když rozhoduje jedno
zařízení, taková otázka nikdy nevznikne. U hry, kde na dvacetině sekundy
nezáleží, má shoda větší cenu než okamžitost.

## Každé zařízení hýbe svým hráčem

Výjimkou je tvůj vlastní pohyb. Kdyby kroky připojeného hráče musely putovat
k hostiteli a zpět, než se pohled pohne, hrálo by se to jako brodění. Každé
zařízení proto hýbe svým hráčem hned a polohu jen hlásí; druhé zařízení toho
hráče ukazuje jako loutku, která klouže za hlášeními.

Znamená to, že hostitel připojenému hráči věří, kde je. Mezi dvěma lidmi na
gauči je to přiměřená míra důvěry.

## Žádat místo dělat

Všechno, co připojený hráč udělá a co mění zápas, je žádost hostiteli:
*vyprdni jeden*, *prdni po čísle 2*, *prdni po příšeře*, *nový zápas*.
Hostitel zkontroluje, co může (jsi v cíli? máš čím zaplatit?), a provede to;
výsledek se vrátí v dalším popisu světa.

V kódu je to pravidlo, které se musí dodržet: zařízení, které se jen
připojilo, nesmí samo měnit skóre, batůžky, prdy, vyřazení ani příšeru. Místa,
která by to dělala – sebrání prdu, vyprdnutí, prdnutí, určení vítěze, celá
mysl příšery, boti – všechna kontrolují, na jakém druhu zařízení běží.

## Efekty se odvozují, neposílají

Hostitel neposílá „tady přehraj zvuk prdu“. Připojené zařízení porovnává každý
popis s předchozím: stouplo skóre, takže někdo vyprdnul – obláček a zvuk
u toho závodníka; změnilo se počitadlo prdnutí, takže se nakreslí proud od něj
k jeho oběti; fáze příšery se změnila na *krmení* a obětí je hráč tohohle
zařízení, takže se spustí lekačka.

Míň druhů zpráv znamená míň způsobů, jak se obě strany můžou rozejít, a
ztracená nebo opožděná zpráva nemůže nechat efekt napůl přehraný: další popis
všechno srovná.

## Společný start

Zařízení se načítají různě rychle. Kdyby hostitel spustil hodiny, zatímco
pomalý telefon ještě staví parkovací dům, měl by hostitel náskok a 45 sekund
příšery by na každé obrazovce znamenalo něco jiného. Hostitel proto zápas
postaví, zmrazí, řekne připojenému zařízení, kde jsou prdy, a čeká na
„připraveno“ (nejdéle dvacet sekund). Zápas má vlastní hodiny, které běží jen
tehdy, když není zmrazený.

Každý zápas má číslo. Zpráva z předchozího zápasu, která dorazí po restartu,
se pozná a zahodí.

## Jak se zařízení najdou

V domácí síti by nikdo neměl muset psát adresu. Hostitel jednou za sekundu
zakřičí krátké oznámení do celé sítě a připojované zařízení vypíše, co slyší.
Některé sítě a telefony taková oznámení spolknou, takže existuje druhý způsob,
který funguje vždycky: hostitel ukáže poslední číslo své adresy jako „kód
hry“, a protože jsou obě zařízení ve stejné síti, připojené zařízení si celou
adresu složí ze své vlastní.

## Bluetooth je jen další drát

Všechno výše popsané putuje jako malé zprávy s délkou na začátku po jednom
spojení. `NetLink` je jedno, co je to za spojení. Přes Wi-Fi je to TCP socket;
mezi dvěma spárovanými telefony s Androidem to může být sériová linka
Bluetooth, kterou obsluhuje malá třída v Javě, protože Unity vlastní Bluetooth
nemá. Hostující telefon čeká na obou a vezme to, co přijde první. Hra nad
drátem se nemění.

Hra přes Bluetooth používá jen spárovaná zařízení. Obejde se tak bez
vyhledávání, které s sebou na Androidu táhne oprávnění k poloze a dialog
o viditelnosti – na hru o prdech příliš mnoho obřadností.

## Když se drát přetrhne

Když připojený hráč zmizí, hostitel ho označí za odešlého a zápas pokračuje;
další zápas je obyčejný sólový. Když zmizí hostitel, nezbývá žádný svět, který
by se dal ukázat, takže se připojené zařízení vrátí do menu a oznámí to.

## Co to není

Je to pro dva hráče v jedné síti nebo v jednom spárování Bluetooth. Nejsou tu
žádné servery, účty, lobby ani hra přes internet; žádné šifrování a žádná
ochrana proti upravené hře; a na obou zařízeních musí běžet stejné sestavení,
protože si verze neporovnávají. Jsou to záměrná omezení hry, kterou hrají lidé
v jedné místnosti.

Přesné bajty jsou na stránce [Síťový protokol](../reference/network-protocol.md).
Co se vyzkoušelo na skutečných zařízeních, je na stránce
[Stav ověření](../reference/verification-status.md).
