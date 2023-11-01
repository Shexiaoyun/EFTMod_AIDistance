import os
def updateFile(file):
    """
    将替换的字符串写到一个新的文件中，然后将原文件删除，新文件改为原来文件的名字
    :param file: 文件路径
    :param old_str: 需要替换的字符串
    :param new_str: 替换的字符串
    :return: None
    """
    with open(file, "r", encoding="utf-8") as f1,open("%s.text" % file, "w", encoding="utf-8") as f2:
        d = {};
        for line in f1:
            line = line.strip()
            
            head = line.partition('=')[0]
            head = head.strip()
            f2.writelines('"' + head + '"' + ',' + '\n')
    #os.rename("%s.bak" % file, file)

updateFile("./temp.txt")