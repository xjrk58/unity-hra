# Vysvětlení: příšera

Zadání bylo jednoduché: něco opravdu strašidelného, co se objeví asi po
45 sekundách zápasu. Tahle stránka vysvětluje rozhodnutí, která stojí za tím,
co vzniklo.

## Věc z plynu

Příšera je vysoký, shrbený přízrak z hustého plynu – rohy, planoucí oči se
štěrbinovými zorničkami, tlama plná jehel, ruce jako hrábě a kouř tam, kde by
byly nohy. Ke hře o prdech se hodí a to, že je z plynu, vyřešilo dva praktické
problémy naráz.

**Nepotřebuje hledání cesty.** Pluje přímo ke své kořisti, skrz sloupy,
zábradlí i rekvizity. Boti potřebují plánovač a opatrné zacházení s rampami,
aby se po parkovacím domě dostali; druhý, větší tvor, který by se proplétal
stejnými mezerami, by byl nejkřehčím kódem v projektu. Takhle je její přímá
čára součástí hrozby: nic, co mezi sebe a ni postavíš, nepomůže.

**Nepotřebuje collider.** Pevná příšera by boty vytlačovala z jejich cest a
zaklínila by je o zábradlí. Takže pevná není vůbec: prdnutí ji „zasáhne“, když
zaměřovací paprsek projde jejím tělem dřív, než narazí na cokoli jiného.

Nepříjemným případem by byly schody. Když kořist opustí patro, udělá příšera
to, co dělá plyn: vsákne se do podlahy a o chvilku později vybublá poblíž
kořisti. Útěkem do jiného patra získáš dvě sekundy, ne bezpečí.

## Strach potřebuje varování a uvolnění

Příšera, která tam prostě je, je nábytek. Celý sled je postavený na tom, co
hráč ví a kdy:

1. **Předzvěst.** Sedm sekund před jejím příchodem se lampy začnou zadrhávat a
   odnikud konkrétně se vynoří hluboké hučení. Dělat se zatím nedá nic; o to
   právě jde.
2. **Příchod.** Všechny lampy na půl sekundy zhasnou, udeří disonantní akord a
   příšera vystoupí z podlahy – ve tvém patře, deset nebo víc metrů daleko, a
   pokud to jde, *za* tebou. Slyšíš ji dřív, než ji vidíš.
3. **Lov.** Její hučení teď má místo, takže ti prozrazuje, kde je. Jak se
   blíží, hráčův vlastní tep se zrychluje a okraje obrazovky tmavnou. HUD
   říká, jak je daleko, ale ne kterým směrem: musíš se podívat.
4. **Chycení.** Pohled se násilím stočí k její tváři, metr od tebe,
   s otevřenými čelistmi a s řevem; pak pro tebe zhasnou světla.
5. **Uvolnění.** Odejde, den se tak úplně nevrátí a ty víš, že přijde znovu.
   Od té chvíle zablikání lamp něco znamená.

Tma dělá dvě práce. Dělá scénu děsivou – svítící oči potřebují kolem sebe tmu
– a hra ti jí sděluje stav příšery bez jediné ikony: noc znamená, že je venku,
soumrak, že je pryč, den, že zápas skončil.

## Dost rychlá, aby na ní záleželo, dost pomalá, aby se jí dalo utéct

Boti chodí rychlostí 4,7 m/s, hráč 6 a sprintuje 8,6. Příšera začíná na 4,9 a
čím déle loví, tím víc zrychluje, až na 6,6.

- Bota nakonec vždycky chytí. Boti před ní neutíkají; dívat se, jak některého
  sežere, je varování i malý dárek pro hráče.
- Hráče, který jde krokem, chytí po pár sekundách. Sprintující hráč vždycky
  unikne – jenže kdo sprintuje pryč, nesbírá prdy.
- Lov skončí nejpozději po 28 sekundách, takže nemůže jednoho hráče držet
  v šachu celý zápas.

Lidem dává přednost v poměru tři ku jedné, protože příšera, která většinou
honí boty, strašidelná není, a nikdy se nevrací rovnou pro toho, koho právě
chytila.

## Něco, co se s tím dá dělat

Čirá bezmoc děsí deset sekund a potom už jen otravuje. Prdnutí – stejné dva
prdy, které vyřadí soupeře – zažene příšeru na čtvrt minuty. Z toho je
rozhodnutí, které něco stojí: prdy utracené za příšeru neodevzdáš a kvůli
míření se k ní musíš otočit čelem.

Chycení stojí celý batůžek a pět sekund. Odevzdané body nestojí nikdy: příšera
může zápas zvrátit, ale ne smazat.

## Zvuk bez zvukového souboru

Její hlas se počítá při startu hry: hučení, tep, akord, řev, zaječení, když je
zahnána. Její vrčení je jedna z dlouhých nahrávek prdů přehraná třikrát
pomaleji a její řev je stejný model prdu, jaký používají záložní zvuky hry,
nafouknutý do absurdní velikosti – zůstává tvorem téhle hry.

Reproduktor telefonu nepřehraje skoro nic pod zhruba 150 Hz a „hluboký“ zvuk
složený jen z hlubokých frekvencí by tam byl tichem. Každý zvuk proto nese
vyšší harmonické, které malé reproduktory přehrát umějí: hučení má nad basem
chrapot a dech, tep má nad dunivým úderem ťuknutí.

## Mysl a tělo zvlášť

Třída, která rozhoduje (`FartMonster`), neví nic o meshích ani o zvuku; drží
si polohu, fázi a kořist. `MonsterVisual` a `MonsterAudio` to jen čtou. Hlava,
čelist a paže jsou samostatné tuhé díly, kterými každý snímek kolem jejich
kloubů otáčí trocha aritmetiky – není tu žádná kostra ani soubor s animací.

Rozdělení se vyplatilo dvakrát. Testy můžou příšeru uvést do libovolného stavu
a vyfotit ji nebo ten stav ověřit. A v zápase pro dva připojené zařízení
pohání stejné tělo a hlas stavem, který posílá hostitel, bez druhé mysli,
která by mohla nesouhlasit.

## Co se zatím neví

Jestli nahání strach, musí posoudit člověk před obrazovkou se zapnutým zvukem,
a tenhle soud zatím nepadl
([Stav ověření](../reference/verification-status.md)). Otázkou je
i načasování: samotní boti dohrají zápas asi za minutu, takže příšera často
dorazí pozdě. Delší zápas by jí dal víc prostoru.
