﻿using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdfRemoveWaterMark
{
    class Pdfium
    {
        public List<WatermarkFound> wartermarkFoundBounds = new List<WatermarkFound>();
        public delegate void AppendLog(string arg, bool isDisplayUI = true);
        private AppendLog appendLog;

        public Pdfium(AppendLog appendLog)
        {
            this.appendLog = appendLog;
        }
        public bool PdfiumSplit(string splitTempFolder, string oriFileName, out string msg)
        {
            try
            {
                msg = string.Empty;
                PdfReader oriFilePdfReader = new PdfReader(oriFileName);
                PdfDocument oriFileDoc = new PdfDocument(oriFilePdfReader);
                if (!Directory.Exists(splitTempFolder))
                {
                    Directory.CreateDirectory(splitTempFolder);
                }

                for (int pageNum = 1; pageNum <= oriFileDoc.GetNumberOfPages(); pageNum++)
                {
                    PdfPage page = oriFileDoc.GetPage(pageNum);

                    // 创建输出PDF文档
                    string outputPdfFilePath = System.IO.Path.Combine(splitTempFolder, $"{pageNum}.pdf");

                    PdfWriter pdfWriter = new PdfWriter(outputPdfFilePath);
                    PdfDocument outputPdfDocument = new PdfDocument(pdfWriter);
                    // 复制当前页到输出文档
                    oriFileDoc.CopyPagesTo(pageNum, pageNum, outputPdfDocument);
                    outputPdfDocument.FlushCopiedObjects(oriFileDoc);
                    appendLog($"Page {pageNum} saved to: {outputPdfFilePath}", false);
                    outputPdfDocument.Close();
                    pdfWriter.Close();
                }
                oriFileDoc.Close();
                oriFilePdfReader.Close();
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                return false;
            }
            finally
            {
            }
            return true;
        }
        public List<WatermarkFound> PdfiumSearchWartermark(string fileName, string[] warterMark, out string msg)
        {
            try
            {   
                PdfReader pdfReader = new PdfReader(fileName);
                PdfDocument document = new PdfDocument(pdfReader);
                for (int pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
                {
                    PdfPage pdfPage = document.GetPage(pageNum);

                    // 创建文本提取策略
                   // SimpleTextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                    var strategy = new CustomTextExtractionStrategy(warterMark);

                    // 创建 PdfCanvasProcessor 对象 // NuGet 处理页面内容 itext7.font-asian
                    PdfCanvasProcessor pdfCanvasProcessor = new PdfCanvasProcessor(strategy);
                    pdfCanvasProcessor.ProcessPageContent(pdfPage);

                    // 获取提取的文本
                    string extractedText = strategy.GetResultantText();

                    // 检查文本是否包含搜索字符串
                    if (strategy.isFound)
                    {
                        // 输出搜索到的文本及其位置信息
                        appendLog($"Found water text on page {pageNum}:{ Environment.NewLine}{string.Join(Environment.NewLine, warterMark)} { Environment.NewLine}", false);
                        List<Rectangle>  bounds = strategy.GetBoundingBoxList();
                        WatermarkFound watermarkFound = new WatermarkFound(pageNum, bounds);
                        wartermarkFoundBounds.Add(watermarkFound);
                    }
                }
            }
            catch (Exception ex)
            {
                appendLog("PdfiumSearchWartermark" + ex.Message);
                msg = ex.Message;
                return null;
            }
            msg = "Search wartermark finished, found count:" + wartermarkFoundBounds.Count;
            return wartermarkFoundBounds;
        }

        class CustomTextExtractionStrategy : LocationTextExtractionStrategy
        {
            private readonly string[] searchTextList;
            public List<Rectangle> searchTextListBounds = new List<Rectangle>();
            public bool isFound = false;

            public CustomTextExtractionStrategy(string[] searchTextList)
            {
                this.searchTextList = searchTextList;
            }

            public override void EventOccurred(IEventData data, EventType type)
            {
                if (type == EventType.RENDER_TEXT)
                {
                    TextRenderInfo renderInfo = (TextRenderInfo)data;
                    string text = renderInfo.GetText();
                    if (text.Length > 4){
                        Console.WriteLine("EventOccurred :"+ text);
                    }
                    for (int i = 0; i < searchTextList.Length; i++)
                    {
                        string searchText = searchTextList[i];
                        if ((searchText.Length >= 1) && text.Contains(searchText))
                        {
                            Rectangle boundingBox = GetTextRectangle(renderInfo);
                            searchTextListBounds.Add(boundingBox);
                            isFound = true;
                        }
                    }
                }
            }

            /// <summary>
            /// 单个文本，如char
            /// </summary>
            /// <returns></returns>
            public List<Rectangle> GetBoundingBoxList()
            {
                return searchTextListBounds;
            }
            /// <summary>
            /// 整个文本块
            /// </summary>
            /// <param name="renderInfo"></param>
            /// <returns></returns>

            private iText.Kernel.Geom.Rectangle GetTextRectangle(TextRenderInfo renderInfo)
            {
                //Matrix textToUserSpaceTransform = renderInfo.GetTextMatrix().Multiply(renderInfo.GetTextMatrix());
                //float x = textToUserSpaceTransform.Get(6);
                //float y = textToUserSpaceTransform.Get(7);
                //float width = renderInfo.GetDescentLine().GetLength();
                //float height = renderInfo.GetAscentLine().GetLength();
                //float allTextLength = renderInfo.GetDescentLine().GetLength();
                return new iText.Kernel.Geom.Rectangle(renderInfo.GetDescentLine().GetBoundingRectangle());
            }
        }

        public bool PdfiumMerge(string removedWarterMarkFolder, string oriFileName, out string msg)
        {
            msg = string.Empty;
            if (!Directory.Exists(removedWarterMarkFolder))
            {
                return false;
            }

            string outputPdfFolder = System.IO.Path.GetDirectoryName(oriFileName);
            if (!Directory.Exists(outputPdfFolder))
            {
                Directory.CreateDirectory(outputPdfFolder);
            }
            string oriFileNameOnly = System.IO.Path.GetFileNameWithoutExtension(oriFileName);
            string outputFileName = System.IO.Path.Combine(outputPdfFolder, $"{oriFileNameOnly}_{DateTime.Now.ToString("yyyy_MM_dd-HHmmss")}.pdf");

            PdfWriter pdfWriter = new PdfWriter(outputFileName);
            PdfDocument pdfDocSave = new PdfDocument(pdfWriter);
            PdfMerger merger = new PdfMerger(pdfDocSave).SetCloseSourceDocuments(true);
            PdfDocument oriFileDocument = new PdfDocument(new PdfReader(oriFileName));
            for (int pageNum = 1; pageNum <= oriFileDocument.GetNumberOfPages(); pageNum++)
            {
                string onePageFilePath = System.IO.Path.Combine(removedWarterMarkFolder, $"{pageNum}.pdf");
                PdfDocument onePageDoc = new PdfDocument(new PdfReader(onePageFilePath));

                merger.Merge(onePageDoc, 1, 1);
                appendLog("\tmerge pageNum " + pageNum);
            }
            PdfiumAddOutLine(oriFileDocument, pdfDocSave);
            merger.Close();
            pdfDocSave.Close();
            oriFileDocument.Close();
            msg = outputPdfFolder;
            return true;
        }

        static void CopyOutlines(PdfDocument sourcePdf, PdfOutline sourceOutline, PdfDocument targetDocument, PdfOutline targetPdfOutlines)
        {
            if (sourceOutline != null)
            {
                // 获取大纲项的标题
                string title = sourceOutline.GetTitle();

                // 在目标PDF文档中添加相应标题的大纲项
                PdfOutline targetOutline = targetPdfOutlines.AddOutline(title);

                // 获取大纲项的目标页码
                int pageNumber = GetPageNumberByTitle(title, sourcePdf);

                if (pageNumber > 0)
                {
                    // 创建目标页的目标 createXYZ 
                    //PdfExplicitDestination destination = PdfExplicitDestination.CreateFit(targetDocument.GetPage(pageNumber));
                    PdfExplicitDestination destination = PdfExplicitDestination.CreateXYZ(targetDocument.GetPage(pageNumber),300, 400, 1.2f);
                    targetOutline.AddDestination(destination);
                }

                // 递归处理子目录项
                foreach (var childSourceOutline in sourceOutline.GetAllChildren())
                {
                    CopyOutlines(sourcePdf, childSourceOutline, targetDocument, targetOutline);
                }
            }
        }

        static int GetPageNumberByTitle(string title, PdfDocument pdfDocument)
        {
            if (string.IsNullOrEmpty(title))
            {
                return -1; // 未找到匹配的页码
            }
            int pageCount = pdfDocument.GetNumberOfPages();
            for (int i = 1; i <= pageCount; i++)
            {
                PdfPage pdfPage = pdfDocument.GetPage(i);
                IList<PdfOutline> outlineList = pdfPage.GetOutlines(false);
                if (outlineList != null)
                {
                    foreach (PdfOutline pdfOutline in outlineList)
                    {
                        if (pdfOutline.GetTitle().Equals(title))
                        {
                            return i;
                        }
                    }
                }
            }
            return -1; // 未找到匹配的页码
        }
        private void PdfiumAddOutLine(PdfDocument sourcePdf, PdfDocument targetPdf)
        {
            sourcePdf.InitializeOutlines();
            targetPdf.InitializeOutlines();
            PdfOutline sourcePdfOutlines = sourcePdf.GetOutlines(false);
            PdfOutline targetPdfOutlines = targetPdf.GetOutlines(true);

            // 复制源PDF文档的大纲到新文档
            CopyOutlines(sourcePdf, sourcePdfOutlines, targetPdf, targetPdfOutlines);
            Console.WriteLine("Outlines copied successfully.");
        }
    }
}
