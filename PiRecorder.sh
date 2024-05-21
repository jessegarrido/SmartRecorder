#!/bin/bash

# PiRecorder:
# Shell script to record mp3 audio and upload it to DropBox

# LEDS, variables, etc


recording=2
net=1
playback=0
curr=$(date +%m)"_"$(date +%d)"_"$(date +%Y)
directory=Desktop/Recordings/$curr
pedal=10
play=6
netblinker_pid=999999
FWD=4
RWD=3
STOP=5
Pause=7
echo "usb" > condition

# setup
setup()
(
#	echo "Refreshing USB devices..."
#	sudo /etc/init.d/udev force-reload
#	wait
	echo "Starting Pi Recording ..."
	gpio mode $Pause up
	gpio mode $FWD up
	gpio mode $RWD up
	gpio mode $STOP up
	gpio mode $pedal up
	gpio mode $play up
	gpio mode $playback out; gpio write $playback 0;
	gpio mode $recording out; gpio mode $net out;
	gpio write $recording 0 ; gpio write $net 0;
	sudo mount -o rw,users /dev/sd*1 /media/usb; wait;
)

# Erase prior recordings?
Eraser()
(
	# exec 3>&2
        # exec 2> /dev/null
	echo  "Press 'record' to delete all saved recordings on SD & USB, and clear upload and play queues, or press 'play' to continue session ..."
	#sleep 8 && cat  blink_pid | xargs kill >/dev/null && echo "Preserving files!" &
	#echo $! > sleeper_pid
	#while [ `gpio read $pedal` = 1 ]; do gpio write $net 1; gpio write $recording 0; sleep 0.1; gpio write $net 0; gpio write $recording 1; sleep 0.1; done && cat sleeper_pid | xargs kill >/dev/null && echo "Deleting files!" && sudo rm -rf Desktop/Recordings/* && sudo rm -rf /media/usb/Recordings/* && > q.txt && > playq.txt && (gpio write $recording 0; sleep 0.25; gpio write $recording 1; sleep 0.25; gpio write recording 0; sleep 0.25; gpio write $recording 1; sleep 0.25; gpio write $recording 0; sleep 0.25; gpio write $recording 1; sleep 0.25) &
	#echo $! > blink_pid
	#until ! (cat sleeper_pid | xargs ps -p) >/dev/null && ! (cat blink_pid | xargs ps -p) >/dev/null; do
		sleep 1
	#done
	while [[ `gpio read $pedal` == 1 && `gpio read $play` == 1 ]]; do sleep 0.1; done &
	blink_pid=$!
	while ps -p $blink_pid> /dev/null; do
		gpio write $playback 1
		gpio write $net 0
		gpio write $recording 0
		sleep .05
		gpio write $playback 0
		gpio write $net 1
		gpio write $recording 0
		sleep .05
		gpio write $playback 0
		gpio write $net 0
		gpio write $recording 1
		sleep .05
		gpio write $playback 0
                gpio write $net 1
                gpio write $recording 0
                sleep .05
	done
	if [ `gpio read $pedal` == 0 ]; then
		gpio write $playback 0
		gpio write $net 0
		gpio write $recording 1
		echo "Deleting Files!"
		sudo rm -rf Desktop/Recordings/* &
		sudo rm -rf /media/usb/Recordings/* &
		> q.txt &
		> playq.txt &
		gpio write $recording 1; sleep 0.5; gpio write $recording 0; sleep 0.25;
		gpio write $recording 1; sleep 0.5; gpio write $recording 0; sleep 0.25;
		gpio write $recording 1; sleep 0.5; gpio write $recording 0; sleep 0.25;
		gpio write $recording 1; sleep 0.5; gpio write $recording 0; sleep 0.25;

	else
		echo "Preserving Files!"
		gpio write $playback 1
                gpio write $net 0
                gpio write $recording 0
		gpio write $playback 1; sleep .5; gpio write $playback 0; sleep 0.25;
                gpio write $playback 1; sleep .5; gpio write $playback 0; sleep 0.25;
                gpio write $playback 1; sleep .5; gpio write $playback 0; sleep 0.25;
		gpio write $playback 1; sleep .5; gpio write $playback 0; sleep 0.25;

	fi
	sleep 1
	# exec 2>&3
        # exec 3>&-
	sudo umount /media/usb
)

# check for internet connection
checknet()
(
	# echo "Checking for internet connetion ..."
	busy=`ps aux | grep -v grep | egrep -c 'arecord|curl'`; wait;
	until [ $busy -eq "0" ]; do sleep 60; done;
		if eval "ping -c 1 google.com" >/dev/null; then
			gpio write $net 1
			echo "net" > condition
			sleep 300
		else gpio write $net 0
			echo "usb" > condition
			sudo ifup --force usb0
			sleep 60
		fi
checknet &
)

# upload recorded files
uploader()
(
	until [ `cat condition` == "net" ]; do
	sleep 30; done;
	until test -s "q.txt"; do
	sleep 3; done;
	#  sudo lame -h --resample 44.1 /media/usb/Recordings/$curr/`head -1 q.txt`.mp3 -b 192 /media/usb/Recordings/$curr/`head -1 q.txt`44.mp3 &
	#q_pid=$!
	#while ps -p $q_pid> /dev/null; do gpio write $net 0; sleep 0.5; gpio write $net 1; sleep 0.5 ; done; wait;
	./dropbox_uploader.sh mkdir Recordings/$curr; wait;
	./dropbox_uploader.sh upload ~/Desktop/Recordings/$curr/`head -1 q.txt`.mp3 Recordings/$curr/`head -1 q.txt`.mp3 && sudo sed -i -e '1d' q.txt &
	q_pid=$!
	while ps -p $q_pid> /dev/null; do gpio write $net 0; sleep 0.5; gpio write $net 1; sleep 0.5 ; done; wait;
	uploader &
)

# wait for pedal to be activated
waitPedalstart()
(
	echo "Waiting for input ... "
	while [[ `gpio read $pedal` == 1 && `gpio read $play` == 1 && `gpio read $STOP` == 1 ]]; do
		sleep 0.1
	done
	if [ `gpio read $pedal` == 0 ]; then
		Recording
	elif [ `gpio read $play` == 0 ]; then
		Playback
		sleep 1
	else
		if [ `gpio read $STOP` == 0 ]; then sleep 4; wait; sudo reboot; while true; do gpio write $recording 1; gpio write $net 1; gpio write $playback 1; done; fi &
		killer_pid=$!
		until [[ `gpio read $STOP` == 1 ]]; do
		gpio write $recording 1; gpio write $net 1; gpio write $playback 1; sleep 0.25; gpio write $recording 0; gpio write $net 0; gpio write $playback 0; sleep 0.25; done;
		kill $killer_pid
		if [ `cat condition` == "net" ]; then gpio write $net 1; fi;
	fi
)

Playback()
(
	gpio write $playback 1
	# find . -name "*.mp3" | sed 's/\.\///;' | sort> playq.txt
	#sort playq.txt> playqtemp; mv playqtemp playq.txt; wait;
	playing=`ps aux | grep -v grep | egrep -c 'mpg123'`; wait;
        if [ $playing -eq "0" ]; then
		track=`tail -1 playq.txt`
		mpg123 --fifo ~/playerpipe -R $track 2>&1 &
		play_pid=$!
		sleep 1; echo LOAD $track > ~/playerpipe
		# wait; echo LOAD $track > ~/playerpipe;
		# echo LOAD `tail -1 playq.txt`> ~/playerpipe
	else
		sleep 1
	fi
	while [[ `kill -0 $play_pid 2>&1` = "" && `gpio read $play` == 1 && `gpio read $FWD` == 1 && `gpio read $RWD` == 1 && `gpio read $STOP` == 1 && `gpio read $Pause` == 1 ]]; 
			do sleep 0.2
		done
	gpio write $playback 0
	if [ `gpio read $play` == 0 ]; then
		sudo killall mpg123 && gpio write $playback 0; wait; Playback;
	elif [ `gpio read $STOP` == 0 ]; then
		sudo killall mpg123 && gpio write $playback 0; wait;
	elif [ `gpio read $Pause` == 0 ]; then
		gpio write $playback 0; echo PAUSE > ~/playerpipe; sleep .5;
		while [[ `gpio read $Pause` == 1 && `gpio read $STOP` == 1 ]]; do
			gpio write $playback 1; sleep .5;
			gpio write $playback 0; sleep .5;
		done
		if [ `gpio read $STOP` == 0 ]; then killall mpg123; gpio write $playback 0; wait;
		else
			echo PAUSE > ~/playerpipe; gpio write $playback 1; sleep .5;
			Playback
		fi
	elif [ `gpio read $RWD` == 0 ]; then
		gpio write $playback 0; sleep 1;
		if [[ `gpio read $RWD` == 1 ]]; then
			sudo killall mpg123; wait;
			echo `tail -1 playq.txt`> tempplayq; wait;
			cat playq.txt>> tempplayq; wait;
			mv tempplayq playq.txt; wait;
			sed -i -e '$d' playq.txt; wait;
			Playback
		else
			while [[ `gpio read $RWD` == 0 ]]; do gpio write $playback 0; echo J -32 > ~/playerpipe; sleep 0.1; gpio write $playback 1; sleep 0.1; done; Playback;  
		fi
	elif [ `gpio read $FWD` == 0 ]; then
		gpio write $playback 0; sleep 1;
		if [[ ` gpio read $FWD` == 1 ]]; then
			sudo killall mpg123; wait;
			echo `head -1 playq.txt` > tempplayq; wait;
			cat tempplayq>>playq.txt; wait;
			sed -i -e '1d' playq.txt; wait;
			Playback
		else
			while [[ `gpio read $FWD` == 0 ]]; do gpio write $playback 0; echo J +32 > ~/playerpipe; sleep 0.1; gpio write $playback 1; sleep 0.1; done; Playback;
		fi
	elif [ $playing -eq "0" ]; then
			Playback
	else
		gpio write $playback 0
	fi
)


# begin recording
Recording()
(
	gpio write $recording 1
	sleep 1
	# sudo mount -o rw,users /dev/sd*1 /media/usb/; wait;
	if [ `cat condition` == "net" ]; then
		curr=$(date +%m)_$(date +%d)_$(date +%Y); wait;
			else
		curr="DATE_UNKNOWN"; wait;
	fi
	# sudo mkdir /media/usb/Recordings
	directory=Desktop/Recordings/$curr; wait;
	if [ ! -d "$directory" ]; then
		sudo mkdir "$directory"
	fi
	wait
	filename=`ls $directory -l | wc -l`; wait;
	filename=$(printf "%02d" $filename); wait;
	#sudo mkdir $directory/temp; wait;
	#exec 3>&2
	#exec 2> /dev/null
	# sudo arecord -f S16_LE -t raw -d 9000 -r 48000 -c 2 -D plughw:1,0 | sudo lame --resample 44.1 -m j -b 192 -s 48 -r - $directory/$filename.mp3 || gpio write $recording 0 &
	# sudo arecord -f S16_LE -t wav -d 9000 -r 48000 -c 2 -D plughw:1,0 $directory/$filename.wav &
	# rec_pid=$!
	echo "Creating new recording: $directory/$filename.mp3"
	parec -d alsa_input.usb-Burr-Brown_from_TI_USB_Audio_CODEC-00-CODEC.analog-stereo --format=s16le --rate=48000 | sudo lame --resample 44.1 -m j -b 192 -s 48 -r - $directory/$filename.mp3 || gpio write $recording 0 &
	# while ps -p $rec_pid > /dev/null; do gpio write $recording 1 && sleep 2; done && gpio write $recording 0 &
	while [[ `gpio read $pedal` == 1 && `gpio read $STOP` == 1 ]]; do
		sleep 0.2
	done
	gpio write $recording 0; sudo killall parec;  wait;
	#exec 2>&3
	#exec 3>&-
	#totalsecs=`cat totalsecs`; wait;
	#totalsecs=`printf %.0f $totalsecs`
	totalsecs=`mp3info -p "%S" $directory/$filename.mp3`; wait;
if [[ totalsecs -lt 5 ]]; then
		sudo rm -f $directory/$filename.mp3; wait; unset totalsecs;
	else
		minutes=`mp3info -p "%m" $directory/$filename.mp3`
		seconds=`mp3info -p "%s" $directory/$filename.mp3`
		seconds=$(printf "%02d" $seconds)
		# runtime=`printf ""%dm%02ds"\n" $(($totalsecs%3600/60)) $(($totalsecs%60))`
		newname="take-"$filename"["$minutes"m"$seconds"s]"$curr
		sudo mv $directory/$filename.mp3 $directory/$newname.mp3; wait;
		unset minutes; unset seconds;
		echo Renamed recording to $newname.mp3 ...
		sudo mount -o rw,users /dev/sd*1 /media/usb/; wait;
		sudo mkdir -p /media/usb/Recordings/$curr; wait;
		sudo cp $directory/$newname.mp3 /media/usb/Recordings/$curr/$newname.mp3 &
		# sudo lame -h --resample 44.1 $directory/$newname.mp3 -b 192 /media/usb/Recordings/$curr/$newname.mp3 &
		usb_pid=$!
      		while ps -p $usb_pid> /dev/null; do gpio write $playback 1; sleep 0.25; gpio write $playback 0; sleep 0.25; done &
		 wait
		 sudo umount /media/usb; wait;
		echo $newname>> q.txt; wait;
		sort playq.txt> playqtemp; mv playqtemp playq.txt; wait;
		echo $directory/$newname.mp3>> playq.txt; wait;
fi
)

# main program

setup
Eraser
checknet &
uploader &
while true; do
	waitPedalstart
done

