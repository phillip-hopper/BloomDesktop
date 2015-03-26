#!/usr/bin/env bash

thisDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

lastFile="${thisDir}/.lastJadeCheck"

if ! [ -f ${lastFile} ]
then
	echo "1409590000" > "$lastFile"
fi

current=`date +%s`
last=`cat ${thisDir}/.lastJadeCheck`
ext=".htm"

# get all *.ts files that are not *.d.ts files
find ${thisDir} -type f -name *.jade -print0 | while read -d $'\0' file; do

	modified=`date -r "${file}" +"%s"`

	if [ ${modified} -gt ${last} ]
	then
		destination="${file/.jade/$ext}"

		../../build/node_modules/.bin/jade --pretty < "$file" > "$destination"

		echo "$destination"
	fi
done

echo "finished"
echo "$current" > "$lastFile"