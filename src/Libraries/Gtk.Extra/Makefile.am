ASSEMBLY = Gtk.Extra
TARGET = library
LINK = $(REF_GTK_EXTRA)

SOURCES =  \
	Gtk/Child.cs \
	Gtk/Extensions.cs \
	Gtk/HeaderBar.cs \
	Gtk/JunctionSides.cs \
	Gtk/PopoverConstraint.cs \
	Gtk/Popover.cs \
	Gtk/PopoverMenu.cs

include $(top_srcdir)/build/build.mk

module_SCRIPTS += Gtk.Extra.dll.config
EXTRA_DIST += Gtk.Extra.dll.config.in
DISTCLEANFILES += Gtk.Extra.dll.config

