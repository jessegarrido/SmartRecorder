#!/bin/bash

sudo sed -i '$d' ".bashrc"; wait;
sudo echo "bash PiRecorder.sh">> .bashrc; wait;
sudo reboot;
