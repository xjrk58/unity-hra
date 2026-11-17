# Vysvětlení: testování bez obrazovky

Většinu téhle hry zkontrolovaly programy, ne hraní. Tahle stránka vysvětluje,
jak to funguje, k čemu je to dobré a kde to končí.

## Myšlenka

Editor Unity umí běžet bez okna. Když ho tak spustíš a řekneš mu, ať vykoná
jednu metodu, dokáže vstoupit do režimu Play, nechat běžet skutečnou hru,
dívat se na ni zvenčí a skončit s verdiktem. Každý „runner“ v `Assets/Editor`
je taková metoda. Protože svět staví kód
([Jak se staví svět](how-the-world-is-built.md)), není potřeba nic předem
naklikat na místo.

Hodiny hry se přepnou na pevné kroky po třicetině sekundy, tak rychle, jak je
počítač stihne počítat. Čtyřminutový zápas trvá výrazně míň než minutu a
pokaždé proběhne stejně.

## Čtyři druhy kontrol

**Zápas, ve kterém nikdo není.** `PlaytestRunner` nechá hrát boty a sleduje
je: sbírají v každém patře, stoupají po rampách, odevzdávají a nikdy dlouho
nestojí na místě? Spočítá každou chybu, kterou hra zaloguje, a pak znovu
načte scénu tak, jak by to udělal hráč stiskem `R`. Spousta chyb je prostě
„bot se zasekl“ nebo „ve třetí minutě něco vyhodilo výjimku“.

**Naskriptované prsty.** Dotykové ovládání se na počítači ručně vyzkoušet
nedá. Hra ale čte prsty přes jednu malou funkci a test ji může nahradit: polož
prst sem, táhni ho tam, polož druhý, zatímco první drží. `TouchTestRunner`
kontroluje, že páčka umí chůzi i sprint, že tah otočí pohled o správný úhel,
že obojí funguje zároveň, že fungují tlačítka a že prst, který začal na
tlačítku, pohled nikdy neotočí.

**Příšera na vodítku.** `MonsterTestRunner` nejdřív počká, až příšera dorazí
sama, a zkontroluje, kdy a kde se to stane. Potom zadními vrátky umístí
příšeru tam, kde ji kontrola potřebuje – vedle hráče, který stojí na místě, do
jiného patra, pod zaměřovač.

**Dva programy, které si povídají.** Pro hru ve dvou běží na jednom počítači
dvě kopie sestavené hry, jedna hostuje a druhá se připojuje, obě hrají samy a
každá zapisuje každou sekundu stavový řádek. Porovnání obou logů ukáže, jestli
se shodují.

## Obrázky jsou důkaz, jen když se na ně někdo podívá

`ShotRunner` vykresluje hru z pevných stanovišť. To, že se soubor zapsal,
nedokazuje nic; na obrázky se někdo musí podívat. Takhle se našly černé
podlahy, chybějící spodní čelist příšery a řádek s autorem zvuku, který
přetékal z obrazovky. HUD potřebuje trik – překryvná vrstva existuje jen na
skutečné obrazovce, takže se pro fotografii zavěsí před kameru.

## Co programy zkontrolovat neumějí

- **Zvuk.** Program umí změřit hlasitost a výšku tónu, ne to, jestli řev
  nahání strach.
- **Pocit.** Jestli je páčka pod palcem pohodlná nebo jestli je příšera tak
  rychlá, že to už není zábava.
- **Skutečná zařízení.** Grafický čip telefonu, jeho oprávnění, jeho zvyk
  opustit Wi-Fi, když zhasne obrazovka.

Každá z těch věcí se aspoň jednou vymstila. Testovací sestavení, které na
počítači vypadalo dobře, na telefonu zamrzlo a telefon pak vypadl ze sítě
dřív, než se dal přečíst jeho log. Poctivý seznam toho, co se nevyzkoušelo, je
[Stav ověření](../reference/verification-status.md).

## Dvě poučení ze samotných testů

**Test se může mýlit.** Test dotykového ovládání jednou selhal, protože hráč
náhodou prošel prdem: prdy leží na náhodných místech a test předpokládal
přesný počet v batůžku. Předtím procházel jen díky štěstí. Kontrola teď
porovnává stav před a po.

**Úklid je součást testu.** První běhy hry ve dvou „selhaly“, protože programy
z předchozího běhu pořád držely síťový port. Se hrou nebylo nic špatně.

## Proč se s tím u malé hry dělat

Protože alternativou není „testovat ručně“, ale „netestovat“. Nikdo po každé
malé změně neprojde hrou každé patro, oba způsoby ovládání, každou fázi
příšery a restart. Minuta počítačového času ano.

Jak to všechno spustit: [Spuštění automatických kontrol](../how-to/run-the-automated-checks.md).
