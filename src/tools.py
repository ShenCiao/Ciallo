import cv2
import numpy as np

def read_image(filename: str):
    """
    Read image data from source image data
    params:
        filename:   filename of source image
    return:
        image_data: image data in ndarray format
    """
    image_data = cv2.imread(filename)

    return image_data

def cvt_gray(image_data: np.ndarray):
    """
    Transfer original image to gray image
    params:
        image_data: image data in ndarray format.
    return:
        image_data: gray image data in ndarray format
    """
    image_data = cv2.cvtColor(image_data, cv2.COLOR_BGR2GRAY)
    
    return image_data

def poly_mask_gen_fill(image_data: np.ndarray, contour: np.ndarray):
    """
    Generate poly mask based on brush controur
    params:
        image_data: original image data
        contour:    Sequence of edge points
    return:
        mask:       mask of the corresponding contour
    """
    mask = np.zeros_like(image_data, np.uint8)
    # Fill the poly area in white
    mask = cv2.drawContours(mask, [contour], -1, (255, 255, 255), cv2.FILLED)
    mask = cv2.cvtColor(mask, cv2.COLOR_RGBA2GRAY)
    _, mask = cv2.threshold(mask, 127, 255, type=cv2.THRESH_BINARY)

    return mask

def get_split_coordinate(mask: np.array):
    min_x = mask.shape[1]
    min_y = mask.shape[0]
    max_x = 0
    max_y = 0
    for y in range(min_y):
        for x in range(min_x):
            if mask[y, x] == 255:
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)
    return min_x, min_y, max_x, max_y


def get_bgr_from_str(input: str):
    bgr_thres =  input[1:-1].split(',')
    blue_thres = int(bgr_thres[0])
    green_thres = int(bgr_thres[1])
    red_thres = int(bgr_thres[2])

    return blue_thres, green_thres, red_thres