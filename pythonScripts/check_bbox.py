import os
import cv2 as cv
import numpy as np
import random as rnd

def read_annotations(img_no):
    annotations = np.genfromtxt("data/gt.csv", delimiter=",")
    annotations = annotations[:, :-1]
    annotations = annotations[annotations[:,0] == img_no]
    #print(annotations)
    return annotations

def draw_rectangles(img, annotations):
    for i in range(0,np.shape(annotations)[0]):
        min_x = int(annotations[i, 2])
        min_y = int(annotations[i, 3])
        max_x = int(annotations[i, 2] + annotations[i, 4])
        max_y = int(annotations[i, 3] + annotations[i, 5])
        center_x = int((min_x + max_x)/2)
        center_y = int((min_y + max_y)/2)
        color = (rnd.randint(0,255), rnd.randint(0,255), rnd.randint(0,255))
        #print("min " + str((min_x, min_y)))
        #print("max " + str((max_x, max_y)))
        cv.rectangle(img, (min_x, min_y), (max_x, max_y), color, 2)
        cv.putText(img, str(annotations[i, 1]), (center_x, center_y), cv.FONT_HERSHEY_DUPLEX, 1, color, 1)
    cv.imshow("test", img)
    cv.waitKey(0)
    return 0

if __name__=="__main__":
    images = os.listdir("data")
    images.remove("gt.csv")
    for i in range(1, len(images)+1):
        img = cv.imread("data/"+str(i)+".png")
        #img_no = int(image[:-4])
        annotations = read_annotations(i)
        draw_rectangles(img, annotations)

        
    
    
    #cv.imshow("test", img)
    #cv.waitKey(0)
