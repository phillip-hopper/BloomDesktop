#!/usr/bin/env bash

thisDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

lastFile="${thisDir}/.lastTscCheck"

if ! [ -f ${lastFile} ]
then
	echo "1409590000" > "$lastFile"
fi

current=`date +%s`
last=`cat ${thisDir}/.lastTscCheck`
ext=".js"

# get all *.ts files that are not *.d.ts files
find ${thisDir} -type f -name *.ts ! -name *.d.ts -print0 | while read -d $'\0' file; do

    modified=`date -r "${file}" +"%s"`
    outDir=`dirname "${file}"`
	if [ ${modified} -gt ${last} ]
	then
		../../build/node_modules/.bin/tsc --sourcemap "$file"  # --outDir ${outDir}

		echo "${file/.ts/$ext}"
	fi
done

echo "finished"
echo "$current" > "$lastFile"
