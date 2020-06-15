# ARPrivacyZones

This package is part of a larger project that allows you to create a privacy zone (or "no-go" region) that a turtlebot won't drive through. It was made using Google's ARCore sdk for Unity and built on top of Google's HelloAR code. 

There are three interfaces available to create the privacy zone:
1. **Physical**: Four real, orange cones are placed on the ground to mark the corners of the no-go region.
2. **Map**: Four points are clicked on a map of the room to mark the corners of the no-go region.
3. **AR**: Four virtual cones are placed on the ground in an app by tapping the screen.

There are three feedback types to see if the privacy zone works:
1. **Physical**: The robot drives around the room and avoids the no-go region.
2. **Map**: The no-go region is shaded red on a map of the room.
3. **AR**: A virtual fence appears on the screen where the no-go region was specified. 

This package allows you to use the *AR* interface with physical, map, or virtual feedback, or to use the *physical* or *map* interfaces with AR feedback.

To use the *physical* interface, see [this package](https://github.com/MarisaJH/color_detection).

## Guide
### Requirements
* [color_detection](https://github.com/MarisaJH/color_detection) for physical and map feedback

### How to Run
1. When you decide roughly where you want to place the cones, stand nearby and take notice of your position in the room. You will need to know this later.  
2. Don’t move from your position. Open the app on the phone. Once it has started, you can move if you want, but remember where you were when the app started.  
3. If you want *physical feedback* (ie, see the robot drive around the private region), do the following:  
    4. At the menu, choose “Virtual to No feedback”  
    5. Move the phone around until white planes appear on the floor. These mark the places where a cone can be placed.  
    6. Place your four cones by touching the screen where you want them to be placed.  
    7. Close the app  
    8. On the turtlebot, open a terminal and type **python amcl_rviz.py**  
    9. Set the robot’s position to where you were in the room when the app started  
    10. Now in another terminal, type **rosrun color_detection cone_transformer.py**  
    11. Once the cone_transformer program finishes, you can close amcl and rviz by typing Ctrl + C in their terminals  
    12. Now in any of the terminals, type **python amcl_rviz.py virtual**  
    13. The zone you made on the phone should appear on the map, and the robot should avoid it  
4. If you want *map feedback*, do the following:  
    5. At the menu, choose “Virtual to No feedback”  
    6. Move the phone around until white planes appear on the floor. These mark the places where a cone can be placed.  
    7. Place your four cones by touching the screen where you want them to be placed.
    8. Close the app  
    9. On the turtlebot, open a terminal and type **python amcl_rviz.py**  
    10. Set the robot’s position to where you were in the room when the app started  
    11. Now in another terminal, type **rosrun color_detection cone_transformer.py**  
    12. Once the cone_transformer program finishes, you can close amcl and rviz by typing Ctrl + C in their terminals  
    13. On the local laptop, open a terminal and type **python toMap.py [-h] [-m MAP] [-t TBOT_PATH] [-r REMOTE_HOST] virtual**  
        - Arguments in brackets are optional but will be needed if the program is not being run with default settings (don’t type the brackets)  
        - *MAP* is the path to your map file. If you don’t want to use cs_lounge.yaml, specify your own map.  
        - *TBOT_PATH* is the path to the color_detection package on the turtlebot. Default is /home/turtlebot/catkin_ws/src/color_detection. If yours is different, provide it.  
        - *REMOTE_HOST* is in the form username@host. This allows files to be copied from the turtlebot to the local laptop.  
    14. Click the “import map data” button and choose "virtualToMap.yaml" in the color_detection/zones directory  
5. If you want *AR feedback* (ie, see virtual walls where the cones were), do the following:
    6. At the menu, choose “Virtual to Virtual Feedback”  
    7. Move the phone around until white planes appear on the floor. These mark the places where a cone can be placed.  
    8. Place your four cones by touching the screen where you want them to be placed.  
    9. Once the four cones are placed, a wall will appear that shows the zone you marked  
    10. Close the app   
    11. If you want to log the positions, do the following (optional, for research purposes):  
        - On the turtlebot, open a terminal and type **python amcl_rviz.py**  
        - Set the robot’s position to where you were in the room when the app started  
        - Now in another terminal, type rosrun color_detection cone_transformer.py  
        - Once the cone_transformer program finishes, you can close amcl and rviz by typing Ctrl + C  
