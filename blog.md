# IL2CPP on HoloLens

Folowing the Unity announcement about deprecating the .NET backend I have been slowly turning my attention towards using IL2CPP instead which will be the only option for Unity HoloLens development moving forwards. 

Just to give a very high-level description of what this means; using the .NET backend would generate a .NET UWP project when building my Unity project. I would work with C# code in Visual Studio and deploy my .NET app to a HoloLens device. When I build an IL2CPP project in Unity it creates a native C++ Visual Studio project which is generated including the C# that you write in your Unity scripts. So effectively converts .NET code into native C++. 

Ther is a managed debugger so you can continue to work with C# in a debugging experience. In the Unity build settings if you check 'Wait for managed Debugger' then when you run the resulting app on the HoloLens it will put up a dialog which will wait giving you a chance to hook up the managed debugger.

< 2 x images here>

I usually open another instance of Visual Studio and choose the menu option Debug > Attach Unity Debugger

< image here>

I can then set breakpoints in my C# scripts as expected. Over the last few Unity versions I have been using this experience has been steadily improving. It seemed initially to be slow and sometimes the debugger wouldn't catch my first-chance exceptions. This works well in 2018.3.0f2 which I am currently using.

## Msal Sample

I was working with a sample that I had previously written using the Microsoft Auth Library which was originally used as an example of delegated auth on HoloLens but I recently extended to also show 'device code flow' which allows the auth to happen on a second device which may be more convenient if typing passwords or codes is required.

< video or images of device code flow>

In order to use the MSAL library I downloaded the Nuget package directly from the Nuget website and then chose the relevant dll to include directly into my Unity project.

The device code auth flow works ok in the Unity editor since it doesn't have the complication of requiring a browser to be present in the app. It also worked using the .NET backend but when I switched over to IL2CPP things stopped working and I was hit with a NullReferenceException.

< image of the exception>

Now without the C# source code for the library (I know I could have gone the route of getting that from Github and debugging with it but this won't always be available so I wanted to explore that) I was a bit stuck with a NullReferenceException - < describe how I found the missing properties >

Now it turns out that the IL2CPP process includes stripping out unused code from the resulting project. This causes a problem for code that uses reflection since it is difficult to detect that it is in use. There is an easy fix however, as Unity have exposed a mechanism to prevent the code stripping. < doc link > So, adding a link.xml file into your Assets folder you can prevent stripping for whole assemblies and/or types. So, I added Microsoft.Identity.Client to this file and set it up to prevent all code stripping. This go tme further along but my code still did not work and I was a bit puzzled as to why not. I was still getting a NullReferenceException. 

At this point I turned to the IL2CPP C++ output project and started to debug this since I was a bit stuck with the managed debugger. I started by following the advice here https://blogs.unity3d.com/2015/05/20/il2cpp-internals-debugging-tips-for-generated-code/ for catching exceptions and viewing the contents of string variables in the debugger. I then switched on first-chance exceptions for native and found the location that was causing the exception:

< image of this >

and this aligns with this issue on the Unity forums 

https://forum.unity.com/threads/uwp-datacontractserializer-fails-to-load-configuration-section.507801/ 

The DataContractJsonSerializer itself uses reflection to access methods it uses which again had been stripped out by IL2CPP. So, the fix was simply to add an entry for System.Runtime.Serialization to the link.xml file and then my app was working again. 

I'm sharing my experience here as I think being aware of these issues and some tips on how to diagnose them will be useful for HoloLens developers who in the past have used the .NET backend but at some point will want to port their apps to a newer version of Unity by choice or to keep up with toolkits, etc.