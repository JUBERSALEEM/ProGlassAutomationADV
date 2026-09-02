#!/usr/bin/env python3
import sys
from xml.dom import minidom
if __name__ == '__main__':
    bad = 0
    for p in sys.argv[1:]:
        try:
            minidom.parse(p); print("ok  ", p)
        except Exception as ex:
            print("FAIL", p, "->", ex); bad += 1
    print(f"\n{len(sys.argv)-1} XAML file(s), {bad} failing")
    sys.exit(1 if bad else 0)
