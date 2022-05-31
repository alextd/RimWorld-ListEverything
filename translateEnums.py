import os
import sys
import re 

print("Script is unfinised. Handle updating instead of rewriting. Handle adding .TypeName. to xml tag")
sys.exit(0)

xmlBegin = """<?xml version="1.0" encoding="utf-8" ?>
<LanguageData>
	
"""
xmlEnd = """	
</LanguageData>"""

tag = "TD."
matchEnumRe = re.compile(r'enum\s+\w+\s*{([^}]*)}', re.MULTILINE)

def doEnums():
	outfile = open("Languages\English\Keyed\AutoEnums.xml", "w")
	outfile.write(xmlBegin)
		
	for dname, dirs, files in os.walk("Source"):
		for fname in files:
			if fname.split(os.extsep)[-1] != "cs":
				continue
			print(f"   ---   FILE: {fname}")
			fpath = os.path.join(dname, fname)
			
			with open(fpath, 'r+') as f:
				for match in re.finditer(matchEnumRe, f.read()):
					process(match, outfile)
	
	#aaand end it.
	outfile.write(xmlEnd)

def process(match, outfile):
	for enumStr in match.group(1).replace(',',' ').split():
		print (enumStr)
		
		print ("	<{1}{0}>{0}</{1}{0}>".format(enumStr, tag), file=outfile)
	
doEnums()