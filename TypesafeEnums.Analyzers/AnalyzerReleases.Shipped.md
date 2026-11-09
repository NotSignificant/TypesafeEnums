## Release 1.0

### New Rules
Rule ID | Category | Severity | Notes
------- | -------- | -------- | -----
TS0001  | Logic    | Error    | Ein Switch-Block darf nur maximal einen 'Default'-Fall enthalten.
TS0002  | Logic    | Error    | Der Zustand wurde in diesem Switch-Block bereits abgedeckt.
TS0003  | Logic    | Error    | Im Case-Block sind nur direkt benannte Enum-Zustände zulässig.
TS0004  | Logic    | Warning  | Fehlende Zustände im Switch-Block.
TS0005  | Logic    | Warning  | Leerer Switch-Block.
TS0099  | Logic    | Info     | Zeigt den Namen des Symbols an, das im Case-Block referenziert wird.
