import AppKit
import Foundation
let base=CommandLine.arguments[1],dest=CommandLine.arguments[2]
let w=3200,h=1700
let bmp=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:w,pixelsHigh:h,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
let context=NSGraphicsContext(bitmapImageRep:bmp)!
NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current=context
func col(_ hex: UInt32)->NSColor { NSColor(calibratedRed:CGFloat((hex>>16)&255)/255,green:CGFloat((hex>>8)&255)/255,blue:CGFloat(hex&255)/255,alpha:1) }
func text(_ s:String,_ x:CGFloat,_ y:CGFloat,_ size:CGFloat,_ color:NSColor,_ bold:Bool=false){
 let attrs:[NSAttributedString.Key:Any]=[.font:bold ? NSFont.systemFont(ofSize:size,weight:.semibold) : NSFont.systemFont(ofSize:size,weight:.regular),.foregroundColor:color]
 (s as NSString).draw(at:NSPoint(x:x,y:y),withAttributes:attrs)
}
func photo(_ n:String,_ x:CGFloat,_ y:CGFloat,_ ww:CGFloat,_ hh:CGFloat){
 let img=NSImage(contentsOfFile:base+"/"+n+".png")!
 let bounds=NSRect(x:x,y:y,width:ww,height:hh)
 NSColor.white.setFill(); NSBezierPath(rect:bounds).fill()
 img.draw(in:bounds,from:NSRect(origin:.zero,size:img.size),operation:.copy,fraction:1)
}
col(0xeee6dc).setFill();NSBezierPath(rect:NSRect(x:0,y:0,width:w,height:h)).fill()
col(0x29352e).setFill();NSBezierPath(rect:NSRect(x:0,y:1500,width:w,height:200)).fill()
text("HEFU  /  HOME DESIGN",82,1578,72,.white,true)
text("LUOYANG AUTUMN  ·  UNITY REAL-TIME INTERIOR",85,1530,27,col(0xe9d4ad))
photo("Hero_LivingDining",80,305,1725,1150)
let p:[(String,String,CGFloat,CGFloat)]=[("Entry_North","NORTH ENTRY",1860,910),("Dining_Cabinet","DINING / CABINET",2540,910),("Living_BirthdayWall","LIVING / SOFA",1860,370),("Balcony_South","SOUTH BALCONY",2540,370)]
for (name,label,x,y) in p { photo(name,x,y,610,410);text(label,x,y-49,26,col(0x38453c),true) }
text("MAIN VIEW  /  DINING TO LIVING",95,254,33,col(0x34443b),true)
col(0xa4a89e).setStroke();let line=NSBezierPath();line.move(to:NSPoint(x:85,y:185));line.line(to:NSPoint(x:3110,y:185));line.lineWidth=2;line.stroke()
text("M4 SHOWCASE    |    TRUE UNITY METAL CAPTURE    |    5 FIXED CAMERAS",90,112,27,col(0x39483f),true)
text("SOURCE IMAGES 2048 × 1365   •   BUILT-IN RENDER PIPELINE   •   NOT AN AI MOCKUP",90,69,22,col(0x677266))
context.flushGraphics();NSGraphicsContext.restoreGraphicsState()
try bmp.representation(using:.png,properties:[:])!.write(to:URL(fileURLWithPath:dest))
print("POSTER_READY \(dest) \(w)x\(h)")
