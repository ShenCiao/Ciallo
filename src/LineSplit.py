"""
This file will be used for spliting the same/similar brushes from the source data image
"""
import cv2
import os
import numpy as np
import tools
import pandas as pd

from sklearn.cluster import DBSCAN, KMeans

def opencv_canny_edge_detection(filename: str, threshold1: float = 100, threshold2:float = 200):
    """
    Use Canny to detect the edge of image. Use Gaussian blur to fill in discontinuous edges
    params:
        filename:   filename of source image
        threshold1: threshold1 of Canny function
        threshold2: threshold2 of Canny function
        save_file:  filename to save edge image
    return:
        image_data: image data in ndarray format
    """
    image_data = tools.read_image(filename=filename)
    ori_image = image_data.copy()
    image_data = tools.cvt_gray(image_data=image_data)
    # Find image edges
    image_data = cv2.Canny(image_data, threshold1, threshold2)
    image_data = cv2.dilate(image_data, (3, 3), 30)
    _, image_data = cv2.threshold(image_data, 128, 255, type=cv2.THRESH_BINARY)
    image_data = cv2.GaussianBlur(image_data, (7, 7), 1.5)
    _, image_data = cv2.threshold(image_data, 1, 255, type=cv2.THRESH_BINARY)
    # Water flood fill
    img_floodfill = image_data.copy()
    h, w = image_data.shape[:2]
    mask = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(img_floodfill, mask, (0, 0), 255)
    # Reverse filled image
    img_floodfill = cv2.bitwise_not(img_floodfill)
    image_data = image_data | img_floodfill
    # Find contours
    contours, _ = cv2.findContours(image_data, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    count = 0
    save_dir = os.path.basename(filename).replace('.', '')
    save_dir = os.path.join(os.path.join(os.path.dirname(os.getcwd()), "data"), save_dir)
    if not os.path.exists(save_dir):
        os.mkdir(save_dir)
    dt_set = []
    # Segment each contour to independent images.
    for contour in contours:
        mask = tools.poly_mask_gen_fill(ori_image.copy(), contour)
        # Get coordinates to split the image
        y_set = [item[0][1] for item in contour]
        x_set = [item[0][0] for item in contour]
        min_x = min(x_set)
        min_y = min(y_set)
        max_x = max(x_set)
        max_y = max(y_set)
        # Create mask
        dst = cv2.bitwise_and(ori_image.copy(), ori_image.copy(), mask=mask)
        # # Transfer background to white
        bg = np.ones_like(ori_image, np.uint8) * 255
        bg = cv2.bitwise_not(bg, bg, mask=mask)
        dst_white = bg + dst
        dst_white = dst_white[min_y: max_y, min_x: max_x]
        dt_set.append(np.reshape(dst_white, (1, (max_y - min_y) * (max_x - min_x) * 3))[0])
        cv2.imwrite(os.path.join(save_dir, str(count) + ".png"), dst_white)
        count += 1
    # DBSCANTest(dt_set)

def bursh_cluster(img_path: str):
    """
    Using DBSCAN algorithm to cluster the storkes in one image.
    The data features contain the overall roughness of brush stroke contours and the dispersion level of brush strokes.
    params:
        img_path:   path of input stroke image. This image should have at least one stroke
    """
    img_count = 15
    dt_set = []
    counts = []
    contours_set = []
    for i in range(img_count):
        img = "{}.png".format(i)
        image_name = os.path.join(img_path, img)
        image = cv2.imread(image_name)
        image_data = tools.cvt_gray(image_data=image)
        ori_image = image_data.copy()
        image_data = cv2.Canny(image_data, 100, 200)
        contours, _ = cv2.findContours(image_data, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        
        _, ori_image = cv2.threshold(ori_image, 128, 255, type=cv2.THRESH_BINARY)
        count_255 = 0
        # Extract the features from each storke
        for i in range(ori_image.shape[0]):
            for j in range(ori_image.shape[1]):
                if ori_image[i, j] == 0:
                    count_255 += 1
        # complexity = 0
        # for contour in contours:
        #     complexity += len(contour)
        # dt_set.append([len(contours), count_255])
        contours_set.append(len(contours))
        counts.append(count_255)
    sum_count = sum(counts)
    sum_contour = sum(contours_set)
    for i in range(img_count):
        dt_set.append([contours_set[i] / sum_contour, counts[i] / sum_count])
    
    data_set = np.array(dt_set)
    # Start cluster
    KM = DBSCAN(eps=0.08, min_samples=1)
    y_pred = KM.fit_predict(data_set)
    print(y_pred)
    
        
def read_brush_stroke(dataset_folder: str):
    """
    Read brushstroke dataset
    params:
        dataset_folder: path of burshstroke dataset
    """
    parameters = os.path.join(dataset_folder, "parameter")
    rendered_images = os.path.join(dataset_folder, "png")

    parameter_file_list = os.listdir(parameters)
    for parameter_filename in parameter_file_list:
        parameter_file = os.path.join(parameters, parameter_filename)
        image_file = os.path.join(rendered_images, parameter_filename.replace('csv', 'png'))
        split_brush_stroke_dataset(image_file, parameter_file, "")
        break



        
def split_brush_stroke_dataset(img_path: str, param_path: str, path_path:str):
    """
    This function is only for extract storke from brushstroke dataset.
    params:
        img_path:   path of rendered image
        param_path: path of corresponding stroke parameters file
    output:
        croped stroke image from rendered image
    """
    img = cv2.imread(img_path)
    params = pd.read_csv(param_path)
    save_path = os.path.basename(img_path).replace('.', '')
    save_dir = os.path.join(os.path.join(os.path.dirname(os.getcwd()), "data"), save_path)
    if not os.path.exists(save_dir):
        os.mkdir(save_dir)
    
    rectangel_set = []
    count_t = 0

    larger = 15
    for i in range(params.shape[0]):
        cur_path = params.loc[i]['Path'].split(';')
        cur_path_set = []
        min_x = 9999
        min_y = 9999
        max_x = 0
        max_y = 0
        for j in range(len(cur_path) // 2):
            count = j * 2
            y = float(cur_path[count]) * 5677
            x = float(cur_path[count + 1]) * 5677
            cur_path_set.append([x, y])
            min_x = min(x, min_x)
            min_y = min(y, min_y)
            max_x = max(x, max_x)
            max_y = max(y, max_y)
        alpha = 1
        min_x = int(min_x * alpha) - larger
        min_y = int(min_y * alpha) - larger
        max_x = int(max_x * alpha) + larger
        max_y = int(max_y * alpha) + larger
        rectangel_set.append([[min_y, min_x], [max_y, max_x]])
        # img = cv2.rectangle(img, [min_y, min_x], [max_y, max_x], color=(0, 0, 255), thickness=5)
        count_t += 1
    non_overlap_rect = find_non_overlapping_rectangles(rectangel_set)
    count = 0
    for rect in non_overlap_rect:
        # img = cv2.rectangle(img, rect[0], rect[1], color=(0, 0, 255), thickness=5)
        cv2.imwrite(os.path.join(save_dir, "{}.png".format(count)), img[rect[0][1]: rect[1][1], rect[0][0]: rect[1][0]])
        count += 1
    # cv2.imwrite("crop.png", img)
    return


    # c_dict = {}
    # for i in range(cut_0[0], cut_1[0]):
    #     for j in range(cut_0[1], cut_1[1]):
    #         if str(img[i, j].tolist()) not in c_dict.keys():
    #             c_dict[str(img[i, j].tolist())] = 1
    #         else:
    #             c_dict[str(img[i, j].tolist())] += 1
    # max_key = ""
    # max_count = 0
    # for key in c_dict.keys():
    #     b, g, r = tools.get_bgr_from_str(key)
    #     if b > 200 and g > 200 and r > 200:
    #         continue
    #     # if key == "[255, 255, 255]":
    #     #     continue
    #     if c_dict[key] > max_count:
    #         max_key = key
    #         max_count = c_dict[key]
    # print(max_key, max_count)

    # blue_thres, green_thres, red_thres = tools.get_bgr_from_str(max_key)
    # print(blue_thres, green_thres, red_thres)


    crop_img = img.copy()[cut_0[1]: cut_1[1], cut_0[0]: cut_1[0]]
    for i in range(crop_img.shape[0]):
        for j in range(crop_img.shape[1]):
            if (crop_img[i, j] > 240).all():
                crop_img[i, j] = 0
    cv2.imwrite("crop.png", crop_img)
    ori_image = crop_img.copy()
    crop_img = cv2.cvtColor(crop_img, cv2.COLOR_BGR2RGB)
    # reshape the image to a 2D array of pixels and 3 color values (RGB)
    crop_img = crop_img.reshape((-1, 3))
    # convert to float
    crop_img = np.float32(crop_img)
    
    criteria = (cv2.TERM_CRITERIA_EPS +
                cv2.TERM_CRITERIA_MAX_ITER, 10, 1.0)
    
    compactness, labels, centers = cv2.kmeans(crop_img, 3, None, criteria, 10, cv2.KMEANS_PP_CENTERS)
    print(compactness, labels, centers)
    for center_index in range(centers.shape[0]):
        cache = ori_image.copy()
        blue_thres = centers[center_index, 2]
        green_thres = centers[center_index, 1]
        red_thres = centers[center_index, 0]
        for i in range(cache.shape[0]):
            for j in range(cache.shape[1]):
                cur_pixel = cache[i, j]
                if abs(cur_pixel[0] - blue_thres) > 10 and abs(cur_pixel[1] - green_thres) > 10 and abs(cur_pixel[2] - red_thres) > 10:
                    cache[i, j] = 0
        cv2.imwrite("crop_{}.png".format(center_index), cache)
    img = cv2.rectangle(img, cut_0, cut_1, (0, 0, 255), 5)
    

def find_non_overlapping_rectangles(rectangles):
    # Step 2: Sort rectangles based on x and y coordinates
    rectangles.sort(key=lambda rect: (rect[0][0], rect[0][1]))

    # Step 3: Initialize the result list with the first rectangle
    result = [rectangles[0]]

    # Step 4: Iterate over remaining rectangles
    for rect in rectangles[1:]:
        # Step 4a: Check if current rectangle overlaps with any rectangle in the result list
        overlap = False
        for r in result:
            if rect[0][0] <= r[1][0] and rect[0][1] <= r[1][1] and rect[1][0] >= r[0][0] and rect[1][1] >= r[0][1]:
                overlap = True
                break

        # Step 4b: Add the rectangle to the result list if it doesn't overlap with any existing rectangle
        if not overlap:
            result.append(rect)

    # Step 5: Return the result list
    return result